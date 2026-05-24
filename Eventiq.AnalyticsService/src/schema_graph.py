"""Build a NetworkX graph of the 5 service schemas.

Nodes are fully-qualified table names (`<schema>.<table>` or
`<schema>."<Table>"`). Edges carry join column metadata in `col_a`/`col_b`.
"""
from __future__ import annotations

import networkx as nx

from .db import BUSINESS_SCHEMAS, connect
from .logical_fk import LOGICAL_FK
from .system_tables import is_business_table

# Schemas where unquoted identifiers are folded to lowercase by Postgres
# but the entities are PascalCase — node names must include quotes.
PASCAL_SCHEMAS = frozenset({"user_service", "org_service"})


def _fq(schema: str, table: str) -> str:
    if schema in PASCAL_SCHEMAS:
        return f'{schema}."{table}"'
    return f"{schema}.{table}"


PHYSICAL_FK_SQL = """
SELECT
  tc.constraint_schema  AS schema_a,
  tc.table_name         AS table_a,
  kcu.column_name       AS col_a,
  ccu.table_schema      AS schema_b,
  ccu.table_name        AS table_b,
  ccu.column_name       AS col_b
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
  ON tc.constraint_name   = kcu.constraint_name
 AND tc.constraint_schema = kcu.constraint_schema
JOIN information_schema.constraint_column_usage ccu
  ON ccu.constraint_name   = tc.constraint_name
 AND ccu.constraint_schema = tc.constraint_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND tc.constraint_schema = ANY(%s);
"""


def load_physical_fk(conn) -> list[tuple[str, str, str, str]]:
    with conn.cursor() as cur:
        cur.execute(PHYSICAL_FK_SQL, (list(BUSINESS_SCHEMAS),))
        rows = cur.fetchall()
    return [(_fq(sa, ta), ca, _fq(sb, tb), cb) for sa, ta, ca, sb, tb, cb in rows]


def build_graph(
    physical_fk: list[tuple[str, str, str, str]] | None = None,
    logical_fk: list[tuple[str, str, str, str]] | None = None,
) -> nx.Graph:
    """Build an undirected NetworkX graph from physical + logical FK."""
    physical_fk = physical_fk if physical_fk is not None else []
    logical_fk = logical_fk if logical_fk is not None else LOGICAL_FK

    g = nx.Graph()
    for table_a, col_a, table_b, col_b in physical_fk + logical_fk:
        if not (is_business_table(table_a) and is_business_table(table_b)):
            continue
        g.add_node(table_a)
        g.add_node(table_b)
        g.add_edge(table_a, table_b, col_a=col_a, col_b=col_b)
    return g


def build_graph_from_db() -> nx.Graph:
    with connect() as conn:
        physical = load_physical_fk(conn)
    return build_graph(physical, LOGICAL_FK)


if __name__ == "__main__":
    g = build_graph_from_db()
    print(f"nodes={g.number_of_nodes()} edges={g.number_of_edges()}")
    for n in sorted(g.nodes()):
        print(f"  {n}")
