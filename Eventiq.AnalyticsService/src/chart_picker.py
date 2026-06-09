"""Stage 7 — pick a *default* chart type and axis mapping for the result set.

`pick_chart` returns a `chart_config` dict the frontend can render
directly, no axis-guessing required:

    {"type": "kpi",     "value": <col>}                  # single scalar
    {"type": "line",    "x": <col>, "y": [<num cols>]}   # time series
    {"type": "bar",     "x": <col>, "y": [<num cols>]}   # categorical default
    {"type": "scatter", "x": <num col>, "y": <num col>}  # 2 numerics
    {"type": "table"}                                     # fallback

The type is chosen purely from the *shape* of the data (column count,
column types, row count) — never from the wording of the question.
"How to display" is a presentation concern decoupled from "what to
fetch", so the heuristic only produces a sensible default; the user
picks the final chart type in the UI (e.g. switching a categorical
bar to a pie). This keeps the backend deterministic and avoids brittle
keyword-sniffing on the question text.

`pick_chart_type` is kept as a thin wrapper returning just the type
string, for callers/tests that only care about the type.
"""
from __future__ import annotations

from datetime import date, datetime
from decimal import Decimal
from typing import Any, Iterable

ChartType = str  # 'table' | 'kpi' | 'line' | 'pie' | 'scatter' | 'bar'

_TIME_TOKENS: tuple[str, ...] = ("month", "date", "year", "day", "time", "week", "quarter")


def _is_numeric(value) -> bool:
    return isinstance(value, (int, float, Decimal)) and not isinstance(value, bool)


def _is_temporal(value) -> bool:
    return isinstance(value, (date, datetime))


def pick_chart(rows: Iterable[dict], question: str) -> dict[str, Any]:
    """Choose a chart type + axis mapping for `rows`."""
    rows = list(rows)
    if not rows:
        return {"type": "table"}

    sample = rows[0]
    cols = list(sample.keys())
    if not cols:
        return {"type": "table"}

    numeric_cols = [c for c in cols if _is_numeric(sample[c])]
    non_numeric = [c for c in cols if c not in numeric_cols]
    first = cols[0]
    first_lower = first.lower()

    # Single-value scalar → KPI card.
    if len(rows) == 1 and len(cols) == 1:
        return {"type": "kpi", "value": first}

    value_cols = [c for c in numeric_cols if c != first]

    # Time-series: first column is a date/time or named like one.
    is_time_first = _is_temporal(sample[first]) or any(tok in first_lower for tok in _TIME_TOKENS)
    if is_time_first and value_cols:
        return {"type": "line", "x": first, "y": value_cols}

    # Categorical label + numeric value(s) → bar (the common case). The user
    # can switch this to a pie in the UI when it reads as a ratio breakdown.
    if non_numeric and value_cols:
        return {"type": "bar", "x": non_numeric[0], "y": value_cols}

    # Two or more numeric columns, no categorical anchor → scatter.
    if len(numeric_cols) >= 2:
        return {"type": "scatter", "x": numeric_cols[0], "y": numeric_cols[1]}

    return {"type": "table"}


def pick_chart_type(rows: Iterable[dict], question: str) -> ChartType:
    """Thin wrapper returning just the chart type string."""
    return pick_chart(rows, question)["type"]
