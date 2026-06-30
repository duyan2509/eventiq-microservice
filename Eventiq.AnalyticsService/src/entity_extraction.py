"""Stage 1 of the Text2SQL pipeline — entity extraction (LLM call #1)
and table-name normalization.

The LLM is given the full list of 25 business tables and a natural
question, and is asked to pick the relevant subset plus filter values,
aggregation, and a confidence score. `normalize_table_name` then maps
loose LLM outputs (`"users"`, `"Tickets"`) back to canonical form
(`user_service."Users"`, `event_service.tickets`).
"""
from __future__ import annotations

import difflib
import json
import re

from . import llm_client
from .schema_dump import SCHEMA

CONFIDENCE_THRESHOLD = 0.7

# Tables exposed to the LLM, grouped for readability in the prompt.
_PROMPT_TABLE_LIST = """\
# user_service (PascalCase — luôn để trong dấu nháy kép)
user_service."Users", user_service."Roles", user_service."UserRoles",
user_service."BanHistories", user_service."RefreshTokens", user_service."PasswordResetTokens"

# org_service (PascalCase)
org_service."Organizations", org_service."Members", org_service."Permissions",
org_service."Invitations", org_service."PlatformConfigs", org_service."PayoutLogs"

# event_service (snake_case)
event_service.events, event_service.sessions, event_service.charts,
event_service.legends, event_service.submissions, event_service.tickets,
event_service.org_payment_info

# seat_service (snake_case)
seat_service.seat_maps, seat_service.seats, seat_service.objects, seat_service.versions

# payment_service (snake_case)
payment_service.orders, payment_service.order_items"""


_PROMPT_TEMPLATE = """\
Bạn là chuyên gia phân tích câu hỏi nghiệp vụ → ánh xạ về bảng SQL.

Danh sách bảng (fully-qualified, GIỮ NGUYÊN case và dấu nháy kép):

{tables}

Câu hỏi: "{question}"

Trả về JSON DUY NHẤT (không markdown, không giải thích, không text thừa):
{{
  "tables": ["tên bảng liên quan, GIỮ NGUYÊN format trên"],
  "filters": ["giá trị filter nếu có (status='Paid', date range, ...)"],
  "aggregation": "SUM|COUNT|AVG|MAX|MIN|null",
  "confidence": 0.0-1.0
}}

Quy tắc ánh xạ từ vựng:
- "users", "khách hàng", "customer" → user_service."Users"
- "organization", "org", "tổ chức" → org_service."Organizations"
- "event", "sự kiện" → event_service.events
- "session", "suất diễn" → event_service.sessions
- "ticket", "vé" → event_service.tickets
- "order", "đơn hàng", "doanh thu" → payment_service.orders
- "seat", "ghế" → seat_service.seats
- "legend", "loại vé", "hạng vé" → event_service.legends

Trả lời confidence thấp (< 0.7) nếu câu hỏi mơ hồ hoặc không khớp bảng nào rõ ràng.
"""


def _build_prompt(question: str) -> str:
    return _PROMPT_TEMPLATE.format(tables=_PROMPT_TABLE_LIST, question=question)


def _parse_json(raw: str) -> dict:
    """Strip optional markdown fence and parse JSON."""
    s = raw.strip()
    s = re.sub(r"^```\w*\n?", "", s)
    s = re.sub(r"\n?```$", "", s)
    # Sometimes LLM prepends "Output:" or similar. Find first '{'.
    brace = s.find("{")
    if brace > 0:
        s = s[brace:]
    return json.loads(s)


# ----------------------------------------------------------- normalize
def normalize_table_name(name: str, all_tables: list[str]) -> str | None:
    """Resolve a loose LLM table reference to canonical FQ form.

    Tier 1: exact match.
    Tier 2: case-insensitive match after stripping quotes.
    Tier 3: suffix match — short name matches the table portion.
    Tier 4: difflib fuzzy fallback (cutoff 0.6).

    Returns None if nothing plausible matches.
    """
    if not name:
        return None
    if name in all_tables:
        return name

    name_clean = name.replace('"', "").lower().strip()
    candidates = {t.replace('"', "").lower(): t for t in all_tables}

    # Tier 2: full FQ match after lowercasing + dequoting.
    if name_clean in candidates:
        return candidates[name_clean]

    # Tier 3: suffix — `users` → `user_service."Users"`.
    suffix = "." + name_clean
    suffix_hits = [orig for clean, orig in candidates.items() if clean.endswith(suffix)]
    if len(suffix_hits) == 1:
        return suffix_hits[0]
    if len(suffix_hits) > 1:
        # Ambiguous suffix (e.g. `status` would not be ambiguous here, but
        # if multiple tables shared a short name across schemas we'd hit
        # this). Pick the lexicographically first to stay deterministic.
        return sorted(suffix_hits)[0]

    # Tier 4: difflib fallback against the short (post-dot) form only.
    # Comparing against full FQ ("event_service.tickets") would dilute the
    # ratio for short LLM outputs ("ticket", "sesion").
    short_to_fq = {
        clean_fq.split(".", 1)[1]: orig for clean_fq, orig in candidates.items()
    }
    needle = name_clean.rsplit(".", 1)[-1]
    matches = difflib.get_close_matches(
        needle, list(short_to_fq.keys()), n=1, cutoff=0.6
    )
    if matches:
        return short_to_fq[matches[0]]
    return None


# ----------------------------------------------------------- extract
def entity_extraction(question: str) -> dict:
    """Run LLM call #1 and return the parsed entity dict.

    Output shape:
        {
          "tables": list[str]            # raw LLM output (not normalized)
          "filters": list[str],
          "aggregation": str | None,
          "confidence": float,
        }
    """
    raw = llm_client.call(_build_prompt(question), max_tokens=200, temperature=0.0,
                          model=llm_client.FAST_MODEL,
                          response_format={"type": "json_object"})
    try:
        parsed = _parse_json(raw)
    except (json.JSONDecodeError, ValueError):
        # A single malformed LLM JSON must not abort an 83-question run. Degrade
        # to an empty entity (routes to keyword-fallback schema linking) instead.
        parsed = {}
    if not isinstance(parsed, dict):
        parsed = {}
    # Soft validation — keep going even if some fields missing.
    parsed.setdefault("tables", [])
    parsed.setdefault("filters", [])
    parsed.setdefault("aggregation", None)
    parsed.setdefault("confidence", 0.0)
    return parsed


def extract_and_normalize(question: str) -> dict:
    """Convenience: entity_extraction + normalize each predicted table.

    Adds `normalized_tables` (unique, no None) to the returned dict.
    """
    result = entity_extraction(question)
    all_tables = list(SCHEMA.keys())
    seen: list[str] = []
    for raw_name in result["tables"]:
        canonical = normalize_table_name(raw_name, all_tables)
        if canonical and canonical not in seen:
            seen.append(canonical)
    result["normalized_tables"] = seen
    return result


async def async_extract_and_normalize(question: str) -> dict:
    """Async variant of `extract_and_normalize` for the streaming pipeline."""
    raw = await llm_client.async_call(
        _build_prompt(question), max_tokens=200, temperature=0.0,
        model=llm_client.FAST_MODEL,
        response_format={"type": "json_object"},
    )
    try:
        parsed = _parse_json(raw)
    except (json.JSONDecodeError, ValueError):
        parsed = {}
    if not isinstance(parsed, dict):
        parsed = {}
    parsed.setdefault("tables", [])
    parsed.setdefault("filters", [])
    parsed.setdefault("aggregation", None)
    parsed.setdefault("confidence", 0.0)
    all_tables = list(SCHEMA.keys())
    seen: list[str] = []
    for raw_name in parsed["tables"]:
        canonical = normalize_table_name(raw_name, all_tables)
        if canonical and canonical not in seen:
            seen.append(canonical)
    parsed["normalized_tables"] = seen
    return parsed


if __name__ == "__main__":
    import sys

    q = " ".join(sys.argv[1:]) or "Doanh thu theo tháng năm nay?"
    out = extract_and_normalize(q)
    print(json.dumps(out, indent=2, ensure_ascii=False))
