"""Compute Spider difficulty from gold SQL string.

Simplified version of Spider's official difficulty classification:
  easy   — simple SELECT, no subquery, ≤1 condition, no GROUP/HAVING/ORDER
  medium — GROUP BY or ORDER BY or LIMIT or 2-4 conditions, no subquery
  hard   — 1 subquery, or INTERSECT/EXCEPT/UNION, or complex conditions
  extra  — 2+ subqueries, or deeply nested
"""
from __future__ import annotations

import re


def _count(pattern: str, sql: str) -> int:
    return len(re.findall(pattern, sql, re.IGNORECASE))


def classify(sql: str) -> str:
    s = sql.upper()

    subquery_count = s.count("SELECT") - 1  # subtract the main SELECT

    has_intersect = bool(re.search(r"\bINTERSECT\b", s))
    has_except = bool(re.search(r"\bEXCEPT\b", s))
    has_union = bool(re.search(r"\bUNION\b", s))
    has_group = bool(re.search(r"\bGROUP\s+BY\b", s))
    has_order = bool(re.search(r"\bORDER\s+BY\b", s))
    has_having = bool(re.search(r"\bHAVING\b", s))
    has_limit = bool(re.search(r"\bLIMIT\b", s))

    join_count = _count(r"\bJOIN\b", s)
    where_cond = _count(r"\b(?:AND|OR)\b", s)

    if subquery_count >= 2 or (subquery_count >= 1 and (has_intersect or has_except or has_union)):
        return "extra"
    if subquery_count == 1 or has_intersect or has_except or has_union or has_having:
        return "hard"
    if has_group or has_order or has_limit or where_cond >= 2 or join_count >= 2:
        return "medium"
    return "easy"
