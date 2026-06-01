"""Stage 7 — pick a Plotly chart type for the result set.

The heuristic only inspects the first row's column names and types.
It is intentionally simple: better-than-nothing defaults, easy to
override in the demo if a question doesn't fit.
"""
from __future__ import annotations

from datetime import date, datetime
from decimal import Decimal
from typing import Iterable

ChartType = str  # 'table' | 'line' | 'pie' | 'scatter' | 'bar'

_TIME_TOKENS: tuple[str, ...] = ("month", "date", "year", "day", "time", "week", "quarter")
_RATIO_TOKENS: tuple[str, ...] = ("tỉ lệ", "tỷ lệ", "phần trăm", "ratio", "%", "tỉ lệ %")


def _is_numeric(value) -> bool:
    return isinstance(value, (int, float, Decimal)) and not isinstance(value, bool)


def _is_temporal(value) -> bool:
    return isinstance(value, (date, datetime))


def pick_chart_type(rows: Iterable[dict], question: str) -> ChartType:
    rows = list(rows)
    if not rows:
        return "table"

    sample = rows[0]
    cols = list(sample.keys())
    if not cols:
        return "table"

    first_col_lower = cols[0].lower()
    q_lower = question.lower()

    # Single-value scalar — show as a table cell.
    if len(rows) == 1 and len(cols) == 1:
        return "table"

    # Time-series: first column is a date/time or named like one.
    if _is_temporal(sample[cols[0]]) or any(tok in first_col_lower for tok in _TIME_TOKENS):
        return "line"

    # Explicit ratio/percentage question → pie.
    if any(tok in q_lower for tok in _RATIO_TOKENS):
        return "pie"

    # Two or more numeric columns + a categorical first column → scatter
    # (rare but useful for "compare two metrics across X" queries).
    numeric_cols = [c for c in cols if _is_numeric(sample[c])]
    if len(numeric_cols) >= 2 and not _is_numeric(sample[cols[0]]):
        return "scatter"

    return "bar"
