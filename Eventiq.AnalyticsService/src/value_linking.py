"""Stage 2.6 — value-level (cell) schema linking (grounded, deterministic).

Runs AFTER table + column linking. For the low-cardinality *category*
columns of the linked tables, it pulls the REAL distinct cell values
straight from the database and checks whether the question literally
mentions one of them. When it does, the (table, column, value) triple is
handed to the SQL generator so it filters on the actual value instead of
guessing — e.g. "ghế VIP" → `event_service.legends.name = 'VIP'` rather
than the model's hallucinated `seat_type = 1`.

Nothing here trusts the LLM: candidate values come from `SELECT DISTINCT`
on the live DB, and matching is plain string containment. High-cardinality
columns (names with many distinct values, free text) are skipped by the
distinct-count guard, so only genuine enumerations are offered. When no
value matches, `covered=False` and the caller adds no value hint.
"""
from __future__ import annotations

import re

from .db import connect
from .schema_columns import column_meta

# Free-text columns that are never useful as a value filter even if they
# happen to be low-cardinality on a small seed DB.
_SKIP_COLS = {"description", "color", "avatar", "banner", "oranization_avatar"}

# Values too generic / structural to anchor a filter on.
_STOP_VALUES = {"none", "null", "n/a", "unknown", "khác", "other"}

_NUMERIC = re.compile(r"^\d+([.,]\d+)?$")

# Cache distinct values per (table, column) — the eval loop asks for the
# same columns across dozens of questions; one query each is plenty.
_distinct_cache: dict[tuple[str, str], list[str] | None] = {}


def _fetch_distinct(fq_table: str, col_token: str, limit: int) -> list[str] | None:
    """Real distinct values for `fq_table.col_token`, or None if the column
    is high-cardinality (more rows than `limit`) / unreadable."""
    key = (fq_table, col_token)
    if key in _distinct_cache:
        return _distinct_cache[key]
    sql = (
        f"SELECT DISTINCT {fq_table}.{col_token} AS v "
        f"FROM {fq_table} "
        f"WHERE {fq_table}.{col_token} IS NOT NULL "
        f"LIMIT {limit + 1}"
    )
    try:
        with connect() as conn, conn.cursor() as cur:
            cur.execute(sql)
            rows = [r[0] for r in cur.fetchall()]
    except Exception:
        rows = None
    result = None if (rows is None or len(rows) > limit) else [str(r) for r in rows]
    _distinct_cache[key] = result
    return result


def _candidate_columns(tables: list[str]) -> list[tuple[str, str]]:
    """(fq_table, col_token) text category columns worth probing for values."""
    meta = column_meta()
    out: list[tuple[str, str]] = []
    for table in tables:
        for name, type_cat, role in meta.get(table, []):
            if type_cat != "text":
                continue
            if role not in ("dimension", "status_enum"):
                continue
            if name.strip('"').lower() in _SKIP_COLS:
                continue
            out.append((table, name))
    return out


def link_values(
    question: str,
    link: dict,
    *,
    fetch=_fetch_distinct,
    max_distinct: int = 50,
    max_values: int = 8,
) -> dict:
    """Return {"values": [(fq_table, col_token, value)], "covered": bool}.

    A value is linked when it appears verbatim (word-bounded, case-insensitive)
    in the question and its column is a low-cardinality text category column on
    one of the already-linked tables.
    """
    q = (question or "").lower()
    matches: list[tuple[str, str, str]] = []
    seen: set[tuple[str, str, str]] = set()

    for fq_table, col_token in _candidate_columns(link.get("tables", [])):
        values = fetch(fq_table, col_token, max_distinct)
        if not values:
            continue
        for value in values:
            v = value.strip().lower()
            if len(v) < 2 or v in _STOP_VALUES or _NUMERIC.match(v):
                continue
            if re.search(rf"(?<!\w){re.escape(v)}(?!\w)", q):
                triple = (fq_table, col_token, value)
                if triple not in seen:
                    seen.add(triple)
                    matches.append(triple)
        if len(matches) >= max_values:
            break

    return {"values": matches[:max_values], "covered": bool(matches)}
