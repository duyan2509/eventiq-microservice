"""Stage 2.5 — column-level schema linking (grounded, deterministic).

Runs AFTER graph table-linking, when the real columns of the chosen
tables are known. Picks the relevant columns by mapping question intent
to column *roles* (measure / temporal / status_enum / dimension / key),
not by trusting an LLM that never saw the columns. Falls back to "no
column constraint" (covered=False) when nothing confident matches, so
the caller can hand the full DDL to the SQL generator instead.
"""
from __future__ import annotations

from .schema_columns import column_meta

# Vietnamese intent cues → which column role to pull in. Kept small on
# purpose; most selection is driven by aggregation + column type/role,
# not by these strings.
_MEASURE_CUES = ("doanh thu", "tiền", "phí", "giá", "revenue", "amount", "tổng tiền", "chi tiêu")
_TEMPORAL_CUES = ("tháng", "ngày", "năm", "quý", "tuần", "thời gian", "gần đây",
                  "month", "date", "year", "quarter", "week")
_STATUS_CUES = ("trạng thái", "status", "thanh toán", "paid", "failed", "pending",
                "duyệt", "approved", "chờ", "đã bán", "sold")
_GROUP_CUES = ("theo từng", "mỗi", "từng", "theo ", "top", "nhiều nhất", "cao nhất")
_DIM_NAMES = ("name", "username", "email", "title")


def _node_columns(graph, table) -> list[tuple[str, str, str]]:
    """(name_token, type_category, role) for a table — from the graph node
    attribute when present, else the static DDL metadata."""
    if graph is not None and table in graph.nodes:
        cols = graph.nodes[table].get("columns")
        if cols:
            return cols
    return column_meta().get(table, [])


def link_columns(question: str, entity: dict, link: dict, graph=None) -> dict:
    tables = link.get("tables", [])
    q = (question or "").lower()
    agg = (entity.get("aggregation") or "").upper()
    filters = " ".join(entity.get("filters") or []).lower()

    selected: list[str] = []

    def add(table: str, name_token: str) -> None:
        ref = f"{table}.{name_token}"
        if ref not in selected:
            selected.append(ref)

    want_measure = agg in {"SUM", "AVG", "MAX", "MIN"} or any(c in q for c in _MEASURE_CUES)
    want_temporal = any(c in q for c in _TEMPORAL_CUES)
    want_status = bool(filters) or any(c in q for c in _STATUS_CUES)
    want_group = any(c in q for c in _GROUP_CUES)

    for table in tables:
        for name, _type, role in _node_columns(graph, table):
            n = name.strip('"').lower()
            if role == "measure" and want_measure:
                add(table, name)
            elif role == "temporal" and want_temporal:
                add(table, name)
            elif role == "status_enum" and want_status:
                add(table, name)
            elif role == "dimension" and want_group and n in _DIM_NAMES:
                add(table, name)
            if n in q or n in filters:                # explicit mention always wins
                add(table, name)

    return {"columns": selected, "covered": bool(selected)}
