"""Regenerate `src/schema_dump.py` from the live DB.

Run after migrations or schema changes:

    python scripts/refresh_schema_dump.py

Pulls columns, types, nullability, and primary keys from
`information_schema` / `pg_constraint`. Merges the per-column
`ANNOTATIONS` table below for enum semantics, snapshot markers, and
known typos. Output is a CREATE TABLE-style string keyed by FQ table
name — exactly the shape the prompt builder expects.
"""
from __future__ import annotations

import sys
from pathlib import Path
from textwrap import indent

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from src.db import BUSINESS_SCHEMAS, connect  # noqa: E402

# PascalCase schemas — table & column names need quoting in SQL.
PASCAL_SCHEMAS = frozenset({"user_service", "org_service"})

from src.system_tables import SYSTEM_TABLES  # noqa: E402

OUT_PATH = Path(__file__).resolve().parents[1] / "src" / "schema_dump.py"


# --- Manual annotations -----------------------------------------------
# Keyed by (fq_table, column) → short comment. Anything not listed gets
# no inline comment, which is fine — the prompt's GIÁ TRỊ ENUM section
# already covers the most important enums separately.
ANNOTATIONS: dict[tuple[str, str], str] = {
    # event_service.events
    ('event_service.events', 'status'):
        '0=Draft 1=Pending 2=Approved 3=Rejected 4=Published 5=Cancelled',
    ('event_service.events', 'oranization_avatar'):
        'SIC: typo in code, keep as-is',
    ('event_service.events', 'organization_name'): 'snapshot',
    ('event_service.events', 'is_deleted'): 'soft delete flag',

    # event_service.legends
    ('event_service.legends', 'price'):
        'integer (NOT numeric) — careful when SUM with orders.total_amount',

    # event_service.tickets
    ('event_service.tickets', 'order_id'):
        'logical FK -> payment_service.orders.id',
    ('event_service.tickets', 'seat_id'):
        'logical FK -> seat_service.seats.id',

    # event_service.org_payment_info
    ('event_service.org_payment_info', 'organization_id'):
        'logical FK -> org_service."Organizations"."Id"',

    # event_service.submissions
    ('event_service.submissions', 'status'):
        'integer enum: 0=Pending 1=Approved 2=Rejected 3=Withdrawn',

    # payment_service.orders
    ('payment_service.orders', 'status'):
        "text enum: 'Pending' | 'Paid' | 'Failed' | 'Refunded'",
    ('payment_service.orders', 'user_id'):
        'logical FK -> user_service."Users"."Id"',
    ('payment_service.orders', 'org_id'):
        'logical FK -> org_service."Organizations"."Id"',
    ('payment_service.orders', 'session_id'):
        'logical FK -> event_service.sessions.id',
    ('payment_service.orders', 'event_name'): 'snapshot',
    ('payment_service.orders', 'session_name'): 'snapshot',
    ('payment_service.orders', 'session_date'): 'snapshot',

    # payment_service.order_items
    ('payment_service.order_items', 'seat_id'):
        'logical FK -> seat_service.seats.id',

    # seat_service.seats
    ('seat_service.seats', 'status'):
        "text enum: 'Available' | 'Holding' | 'Sold' | 'Blocked'",
    ('seat_service.seats', 'seat_type'): '1..4',
    ('seat_service.seats', 'legend_id'):
        'logical FK -> event_service.legends.id',

    # seat_service.seat_maps
    ('seat_service.seat_maps', 'session_id'):
        'NULL=template, NOT NULL=per-session clone',
    ('seat_service.seat_maps', 'status'):
        "text enum: 'Draft' | 'Published' | 'Archived'",

    # org_service.Organizations
    ('org_service."Organizations"', 'PaymentStatus'):
        'integer enum: 0=NotConfigured 1=Pending 2=Active 3=Restricted',

    # org_service.Invitations
    ('org_service."Invitations"', 'Status'):
        'integer enum: 0=Pending 1=Accepted 2=Rejected 3=Expired',

    # user_service.Roles
    ('user_service."Roles"', 'Name'):
        "'Admin' | 'User' | 'Staff' | 'Organization'",

    # user_service.UserRoles
    ('user_service."UserRoles"', 'OrganizationId'):
        'nullable — cross-org role assignment',

    # global soft delete
    ('user_service."Users"', 'IsDeleted'): 'soft delete flag',
}


# --- Helpers ----------------------------------------------------------
def fq(schema: str, table: str) -> str:
    return f'{schema}."{table}"' if schema in PASCAL_SCHEMAS else f"{schema}.{table}"


def quote_ident(schema: str, name: str) -> str:
    """Quote identifier when the owning schema uses PascalCase."""
    return f'"{name}"' if schema in PASCAL_SCHEMAS else name


def fmt_type(data_type: str, char_max: int | None, num_precision: int | None,
             num_scale: int | None) -> str:
    """Map information_schema type to compact Postgres syntax."""
    t = data_type.lower()
    if t == "timestamp with time zone":
        return "timestamptz"
    if t == "timestamp without time zone":
        return "timestamp"
    if t == "character varying":
        return f"varchar({char_max})" if char_max else "varchar"
    if t == "numeric":
        if num_precision and num_scale is not None:
            return f"numeric({num_precision},{num_scale})"
        return "numeric"
    if t == "boolean":
        return "bool"
    if t == "integer":
        return "int"
    if t == "bigint":
        return "bigint"
    if t == "smallint":
        return "smallint"
    if t == "uuid":
        return "uuid"
    if t == "text":
        return "text"
    if t == "jsonb":
        return "jsonb"
    if t == "json":
        return "json"
    if t == "bytea":
        return "bytea"
    return t  # fallback


# --- DB queries -------------------------------------------------------
COLUMNS_SQL = """
SELECT table_schema, table_name, column_name, ordinal_position,
       data_type, character_maximum_length,
       numeric_precision, numeric_scale,
       is_nullable
FROM information_schema.columns
WHERE table_schema = ANY(%s)
ORDER BY table_schema, table_name, ordinal_position
"""

PK_SQL = """
SELECT
  tc.table_schema, tc.table_name, kcu.column_name
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
  ON tc.constraint_name = kcu.constraint_name
 AND tc.constraint_schema = kcu.constraint_schema
WHERE tc.constraint_type = 'PRIMARY KEY'
  AND tc.table_schema = ANY(%s)
"""


def load_schema(conn) -> dict[str, list[dict]]:
    """Return {fq_table: [{name, type, nullable, is_pk}, ...]}."""
    with conn.cursor() as cur:
        cur.execute(PK_SQL, (list(BUSINESS_SCHEMAS),))
        pk_set = {(s, t, c) for s, t, c in cur.fetchall()}
        cur.execute(COLUMNS_SQL, (list(BUSINESS_SCHEMAS),))
        rows = cur.fetchall()

    tables: dict[str, list[dict]] = {}
    for sch, tbl, col, _ord, dtype, char_max, prec, scale, nullable in rows:
        if tbl in SYSTEM_TABLES:
            continue
        key = fq(sch, tbl)
        tables.setdefault(key, []).append({
            "schema": sch,
            "table": tbl,
            "name": col,
            "type": fmt_type(dtype, char_max, prec, scale),
            "nullable": nullable == "YES",
            "is_pk": (sch, tbl, col) in pk_set,
        })
    return tables


# --- Render -----------------------------------------------------------
def render_table(fq_name: str, cols: list[dict]) -> str:
    schema = cols[0]["schema"]
    name_w = max(len(quote_ident(schema, c["name"])) for c in cols)
    type_w = max(len(c["type"]) for c in cols)

    lines: list[str] = [f"CREATE TABLE {fq_name} ("]
    for i, c in enumerate(cols):
        ident = quote_ident(schema, c["name"]).ljust(name_w)
        type_str = c["type"].ljust(type_w)
        flags = []
        if c["is_pk"]:
            flags.append("PRIMARY KEY")
        elif not c["nullable"]:
            flags.append("NOT NULL")
        # Inline annotation
        ann = ANNOTATIONS.get((fq_name, c["name"]))
        comment = f"  -- {ann}" if ann else ""

        flag_str = " ".join(flags)
        sep = "," if i < len(cols) - 1 else ""
        body = f"  {ident}  {type_str}  {flag_str}".rstrip()
        lines.append(f"{body}{sep}{comment}")
    lines.append(");")
    return "\n".join(lines)


def render_module(tables: dict[str, list[dict]]) -> str:
    header = '''"""DDL cache for ~25 business tables across the 5 service schemas.

AUTO-GENERATED by scripts/refresh_schema_dump.py — do not edit by hand.
Re-run the script after migrations. Manual notes live in the script's
`ANNOTATIONS` dict.

Excluded by design (see system_tables.py): MassTransit + EF metadata.
"""
from __future__ import annotations

# Each value is a CREATE TABLE-style block. The prompt builder loads
# the DDL of relevant tables and pastes them verbatim into the LLM
# prompt context.

SCHEMA: dict[str, str] = {
'''
    entries: list[str] = []
    for fq_name in sorted(tables, key=lambda k: (k.split(".")[0], k)):
        body = render_table(fq_name, tables[fq_name])
        entries.append(f'    {fq_name!r}: """\\\n{body}""",')

    footer = '''
}


def get_ddl(table_fq: str) -> str:
    return SCHEMA[table_fq]


def all_tables() -> list[str]:
    return list(SCHEMA.keys())
'''
    return header + "\n".join(entries) + footer


def main() -> int:
    with connect() as conn:
        tables = load_schema(conn)
    if len(tables) != 25:
        print(f"WARN: expected 25 business tables, got {len(tables)}:")
        for k in sorted(tables):
            print(f"  {k}")

    OUT_PATH.write_text(render_module(tables), encoding="utf-8")
    print(f"Wrote {len(tables)} tables to {OUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
