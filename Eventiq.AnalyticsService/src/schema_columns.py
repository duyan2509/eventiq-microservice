from __future__ import annotations

import re
from functools import lru_cache

from .schema_dump import SCHEMA

# name token | type token (numeric(18,2) / double precision / character varying) | rest + comment
_COL_LINE = re.compile(
    r'^\s+("[^"]+"|\w+)\s+'
    r'([a-z]+(?:\([\d,]+\))?(?:\s+(?:precision|varying))?)'
    r'(.*)$'
)


def _type_category(type_str: str) -> str:
    t = type_str.lower()
    if "timestamp" in t or t in ("date",):
        return "temporal"
    if t.startswith(("numeric", "int", "bigint", "smallint", "double", "real", "decimal")):
        return "numeric"
    return "text"


_MEASURE_NAME = re.compile(r"(amount|price|fee|total|revenue|qty|quantity|count)", re.I)


def _role(name_token: str, type_cat: str, comment: str) -> str:
    name = name_token.strip('"').lower()
    if name == "id" or name.endswith("id"):
        return "key"
    if type_cat == "temporal":
        return "temporal"
    if "status" in name or "enum" in comment.lower():
        return "status_enum"
    if type_cat == "numeric" and _MEASURE_NAME.search(name):
        return "measure"
    return "dimension"


def _parse(ddl: str) -> list[tuple[str, str, str]]:
    """Return [(name_token, type_category, role)] for one CREATE TABLE block."""
    out = []
    for line in ddl.splitlines():
        if line.lstrip().startswith(("CREATE", ")", "--")):
            continue
        m = _COL_LINE.match(line)
        if not m:
            continue
        name, type_str, rest = m.group(1), m.group(2), m.group(3)
        comment = rest.split("--", 1)[1] if "--" in rest else ""
        out.append((name, _type_category(type_str), _role(name, _type_category(type_str), comment)))
    return out


@lru_cache(maxsize=1)
def column_meta() -> dict[str, list[tuple[str, str, str]]]:
    """{table: [(name_token, type_category, role)]} for all business tables."""
    return {table: _parse(ddl) for table, ddl in SCHEMA.items()}


def columns_of(table: str) -> list[str]:
    return [name for name, _t, _r in column_meta().get(table, [])]


def column_index(tables: list[str]) -> dict[str, list[tuple[str, str]]]:
    """dequoted-lowercase column name → [(table, canonical_token), ...] over `tables`."""
    idx: dict[str, list[tuple[str, str]]] = {}
    meta = column_meta()
    for t in tables:
        for name, _t, _r in meta.get(t, []):
            idx.setdefault(name.strip('"').lower(), []).append((t, name))
    return idx
