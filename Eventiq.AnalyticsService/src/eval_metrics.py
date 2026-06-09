from __future__ import annotations

from typing import Any, Sequence

Row = dict[str, Any]


def normalize(rows: Sequence[Row] | None) -> list[tuple[str, ...]]:
    if not rows:
        return []
    return sorted(
        tuple(sorted(_stringify(v) for v in row.values()))
        for row in rows
    )


def _stringify(value: Any) -> str:
    if isinstance(value, bool):
        return str(value)
    if isinstance(value, float) and value.is_integer():
        return str(int(value))
    s = str(value)
    if _looks_numeric(s):
        return _trim_numeric(s)
    return s


def _looks_numeric(s: str) -> bool:
    try:
        float(s)
        return True
    except (ValueError, TypeError):
        return False


def _trim_numeric(s: str) -> str:
    if "." not in s:
        return s
    s = s.rstrip("0").rstrip(".")
    return s or "0"


def result_match(gold_rows: Sequence[Row] | None,
                 pred_rows: Sequence[Row] | None) -> bool:
    return normalize(gold_rows) == normalize(pred_rows)
