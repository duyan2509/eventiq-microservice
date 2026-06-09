"""Stage 4 — assemble the SQL-generation prompt.

The prompt has six sections, in order:

1. System instruction (terse, SQL-only, no markdown).
2. Naming convention rules (PascalCase quoting vs snake_case).
3. Enum value table (LLM forgets these otherwise).
4. DDL of the tables in the subgraph (verbatim from `schema_dump`).
5. JOIN hints from schema linking — direction-aware.
6. Few-shot examples covering the most common patterns.

Finally, the question is appended and the model is asked to emit SQL.
"""
from __future__ import annotations

from typing import Iterable

_SYSTEM = (
    "Bạn là chuyên gia SQL PostgreSQL. CHỈ trả về 1 câu SQL hoàn chỉnh, "
    "kết thúc bằng dấu chấm phẩy. KHÔNG giải thích, KHÔNG markdown, "
    "KHÔNG bọc ```sql."
)

_NAMING_RULES = """\
QUY TẮC NAMING (rất quan trọng):
- Bảng/cột thuộc schema user_service và org_service: PascalCase, BẮT BUỘC quote bằng ".."
  Ví dụ: user_service."Users"."Id", org_service."Organizations"."Name"
- Bảng/cột thuộc schema event_service, seat_service, payment_service: snake_case, KHÔNG quote
  Ví dụ: payment_service.orders.user_id, event_service.events.id
- KHÔNG được dùng `fdw_*` prefix.
- KHÔNG được dùng tên bảng ở schema khác (vd `events` không prefix).
"""

_ENUMS = """\
GIÁ TRỊ ENUM:
- event_service.events.status (int):     0=Draft, 1=Pending, 2=Approved, 3=Rejected, 4=Published, 5=Cancelled
- event_service.submissions.status (int): 0=Pending, 1=Approved, 2=Rejected, 3=Withdrawn
- org_service."Organizations"."PaymentStatus" (int): 0=NotConfigured, 1=Pending, 2=Active, 3=Restricted
- org_service."Invitations"."Status" (int):  0=Pending, 1=Accepted, 2=Rejected, 3=Expired
- payment_service.orders.status (text):  'Pending' | 'Paid' | 'Failed' | 'Refunded'
- seat_service.seats.status (text):      'Available' | 'Holding' | 'Sold' | 'Blocked'
- seat_service.seat_maps.status (text):  'Draft' | 'Published' | 'Archived'
- user_service."Roles"."Name" (text):    'Admin' | 'User' | 'Staff' | 'Organization'
"""

_SOFT_DELETE = """\
SOFT DELETE: hầu hết bảng có cột is_deleted / "IsDeleted". Khi đếm hoặc thống kê \
bản ghi "đang hoạt động", thêm điều kiện loại bỏ bản ghi đã xoá (vd `WHERE NOT is_deleted`).
"""

_FEW_SHOT = """\
Few-shot ví dụ:

-- Câu hỏi: Doanh thu theo tháng?
SELECT DATE_TRUNC('month', o.paid_at) AS month,
       SUM(o.total_amount)            AS revenue
FROM payment_service.orders o
WHERE o.status = 'Paid'
GROUP BY 1
ORDER BY 1;

-- Câu hỏi: Top 5 sự kiện bán nhiều vé nhất?
SELECT e.name, COUNT(t.id) AS sold
FROM event_service.events    e
JOIN event_service.sessions  s ON s.event_id   = e.id
JOIN event_service.tickets   t ON t.session_id = s.id
GROUP BY e.id, e.name
ORDER BY sold DESC
LIMIT 5;

-- Câu hỏi: Có bao nhiêu khách hàng đăng ký tháng này?
SELECT COUNT(*) AS new_users
FROM user_service."Users" u
WHERE u."CreatedAt" >= DATE_TRUNC('month', CURRENT_DATE)
  AND NOT u."IsDeleted";

-- Câu hỏi: Sự kiện nào đã được approve trong tuần qua?
SELECT e.name, e.start_time
FROM event_service.events e
WHERE e.status = 2                                         -- Approved
  AND e.updated_at >= CURRENT_DATE - INTERVAL '7 days';

-- Câu hỏi: Org nào doanh thu cao nhất quý này?
SELECT org."Name", SUM(o.total_amount) AS revenue
FROM payment_service.orders o
JOIN org_service."Organizations" org ON org."Id" = o.org_id
WHERE o.status = 'Paid'
  AND o.paid_at >= DATE_TRUNC('quarter', CURRENT_DATE)
GROUP BY org."Id", org."Name"
ORDER BY revenue DESC
LIMIT 1;
"""


# Best-practice rules, added only under `enrich` (keeps the baseline prompt
# byte-identical). Domain-agnostic — not tuned to specific questions.
_QUERY_RULES = """\
QUY TẮC TRUY VẤN (best practice — áp dụng khi phù hợp):
- Khi thống kê/đếm THEO TỪNG nhóm (mỗi tổ chức, mỗi sự kiện, mỗi khách hàng...): \
LEFT JOIN từ bảng nhóm sang bảng chi tiết để GIỮ cả nhóm có 0 bản ghi \
(KHÔNG để INNER JOIN làm rớt nhóm có count = 0).
- Chỉ SELECT đúng các cột câu hỏi cần; KHÔNG thêm cột mô tả thừa.
- Đếm số đối tượng duy nhất: dùng COUNT(DISTINCT ...).
- Tỉ lệ/phần trăm: ép numeric khi chia và chặn chia 0, vd `x::numeric / NULLIF(y, 0)`.
"""

# Few-shot for SQL constructs (window, HAVING, NOT EXISTS, ratio). Scenarios
# are disjoint from the eval questions — they teach the pattern, not the test.
_FEW_SHOT_PATTERNS = """\

-- Câu hỏi: Đánh số thứ tự các suất diễn theo thời gian trong mỗi sự kiện?
SELECT s.event_id, s.id,
       ROW_NUMBER() OVER (PARTITION BY s.event_id ORDER BY s.start_time) AS seq
FROM event_service.sessions s;

-- Câu hỏi: Những tổ chức có nhiều hơn 5 sự kiện?
SELECT e.organization_id, COUNT(*) AS event_count
FROM event_service.events e
WHERE NOT e.is_deleted
GROUP BY e.organization_id
HAVING COUNT(*) > 5;

-- Câu hỏi: Tổ chức nào chưa tạo sự kiện nào?
SELECT org."Id", org."Name"
FROM org_service."Organizations" org
WHERE NOT org."IsDeleted"
  AND NOT EXISTS (
    SELECT 1 FROM event_service.events e WHERE e.organization_id = org."Id"
  );

-- Câu hỏi: Tỉ lệ đơn đã thanh toán trên tổng số đơn?
SELECT COUNT(*) FILTER (WHERE o.status = 'Paid')::numeric
       / NULLIF(COUNT(*), 0) AS paid_ratio
FROM payment_service.orders o;
"""


def _ddl_section(tables: Iterable[str], schema: dict[str, str]) -> str:
    blocks = []
    for fq in tables:
        ddl = schema.get(fq)
        if ddl:
            blocks.append(ddl)
    if not blocks:
        return "(không có DDL — LLM tự suy luận theo tên bảng/cột)"
    return "\n\n".join(blocks)


def _join_section(hints: Iterable[str]) -> str:
    hints = list(hints)
    if not hints:
        return "(không có JOIN hint từ graph — LLM tự đoán JOIN)"
    return "\n".join(f"- {h}" for h in hints)


def _column_section(columns) -> str:
    cols = list(columns or [])
    if not cols:
        return ""
    listed = "\n".join(f"- {c}" for c in cols)
    return (
        "CỘT LIÊN QUAN (column linking): chỉ dùng các cột dưới đây cho SELECT/WHERE/"
        "GROUP BY/ORDER BY. Cột khóa JOIN ở mục JOIN được phép dùng thêm. KHÔNG thêm "
        "cột mô tả thừa ngoài yêu cầu của câu hỏi.\n"
        f"{listed}\n\n"
    )


def _value_section(values) -> str:
    vals = list(values or [])
    if not vals:
        return ""
    listed = "\n".join(f"- {tbl}.{col} = '{val}'" for tbl, col, val in vals)
    return (
        "GIÁ TRỊ THỰC TẾ TRONG DB (value linking): các từ trong câu hỏi khớp ĐÚNG với "
        "giá trị có thật trong cột dưới đây. Khi lọc theo những từ này, BẮT BUỘC dùng "
        "đúng cột và đúng giá trị (đã lấy trực tiếp từ DB), KHÔNG tự đoán mã số/enum khác.\n"
        f"{listed}\n\n"
    )


def build_prompt(
    question: str,
    subgraph: dict,
    schema: dict[str, str],
    columns=None,
    values=None,
    enrich: bool = False,
) -> str:
    """Render the full prompt for SQL generation.

    `subgraph` is the dict produced by `schema_linking.schema_link`,
    i.e. it must have `tables: list[str]` and `join_hints: list[str]`.
    `columns` (optional) is the validated `table.col` list from
    `column_linking.link_columns`; when present the model is told to
    restrict SELECT/WHERE to those columns.
    """
    ddl_block = _ddl_section(subgraph.get("tables", []), schema)
    join_block = _join_section(subgraph.get("join_hints", []))
    rules_block = f"{_QUERY_RULES}\n" if enrich else ""
    few_shot = _FEW_SHOT + (_FEW_SHOT_PATTERNS if enrich else "")

    return (
        f"{_SYSTEM}\n\n"
        f"{_NAMING_RULES}\n"
        f"{_ENUMS}\n"
        f"{_SOFT_DELETE}\n"
        f"{rules_block}"
        f"Schema (chỉ dùng các bảng/cột dưới đây):\n{ddl_block}\n\n"
        f"Quan hệ JOIN (ưu tiên dùng đúng các điều kiện này):\n{join_block}\n\n"
        f"{_column_section(columns)}"
        f"{_value_section(values)}"
        f"{few_shot}\n"
        f"Câu hỏi: {question}\n"
        f"SQL:"
    )
