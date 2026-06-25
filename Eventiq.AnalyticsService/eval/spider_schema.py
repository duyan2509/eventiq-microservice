"""Parse Spider tables.json into NetworkX graph + DDL per database.

Spider column_names_original format:
  [[-1, "*"], [table_idx, col_name], ...]   index 0 is always the wildcard, skip it
  column_types has same length as column_names_original
  primary_keys: list of indices into column_names_original
  foreign_keys: list of [col_idx_a, col_idx_b] pairs
"""
from __future__ import annotations

import json
from pathlib import Path

import networkx as nx

_TYPE_MAP = {
    "text": "TEXT",
    "number": "REAL",
    "time": "TEXT",
    "boolean": "INTEGER",
    "others": "TEXT",
}


def _spider_type(t: str) -> str:
    return _TYPE_MAP.get(t.lower(), "TEXT")


def build_db_schema(db_info: dict) -> dict:
    """Convert a single Spider DB entry into DDL map + NetworkX graph.

    Returns
    -------
    dict with keys:
      table_names: list[str]
      ddl: dict[table_name -> CREATE TABLE statement]
      graph: nx.Graph (nodes = table names, edges = FK relationships)
    """
    tables = db_info["table_names_original"]
    col_names = db_info["column_names_original"]   # [table_idx, col_name]
    col_types = db_info["column_types"]
    pkeys = set(db_info.get("primary_keys", []))
    fkeys = db_info.get("foreign_keys", [])

    # Group columns by table index
    table_cols: dict[int, list[tuple[int, str, str]]] = {i: [] for i in range(len(tables))}
    for idx, (t_idx, col_name) in enumerate(col_names):
        if t_idx < 0:   # wildcard column "*"
            continue
        col_type = _spider_type(col_types[idx])
        is_pk = idx in pkeys
        table_cols[t_idx].append((idx, col_name, col_type, is_pk))

    # Build DDL for each table
    ddl: dict[str, str] = {}
    for t_idx, table_name in enumerate(tables):
        cols = table_cols[t_idx]
        lines = []
        pk_cols = [col_name for _, col_name, _, is_pk in cols if is_pk]

        for _, col_name, col_type, is_pk in cols:
            suffix = " PRIMARY KEY" if (is_pk and len(pk_cols) == 1) else ""
            lines.append(f"  {col_name} {col_type}{suffix}")

        if len(pk_cols) > 1:
            lines.append(f"  PRIMARY KEY ({', '.join(pk_cols)})")

        # Inline FK constraints for documentation clarity
        for fk_from, fk_to in fkeys:
            if fk_from < len(col_names) and fk_to < len(col_names):
                from_t, from_col = col_names[fk_from]
                to_t, to_col = col_names[fk_to]
                if from_t == t_idx:
                    lines.append(
                        f"  FOREIGN KEY ({from_col}) REFERENCES {tables[to_t]}({to_col})"
                    )

        ddl[table_name] = (
            f"CREATE TABLE {table_name} (\n" + ",\n".join(lines) + "\n);"
        )

    # Build NetworkX graph
    g: nx.Graph = nx.Graph()
    g.add_nodes_from(tables)
    for fk_from, fk_to in fkeys:
        if fk_from >= len(col_names) or fk_to >= len(col_names):
            continue
        from_t_idx, from_col = col_names[fk_from]
        to_t_idx, to_col = col_names[fk_to]
        if from_t_idx < 0 or to_t_idx < 0:
            continue
        from_table = tables[from_t_idx]
        to_table = tables[to_t_idx]
        g.add_edge(
            from_table,
            to_table,
            cols={from_table: from_col, to_table: to_col},
        )

    return {
        "table_names": tables,
        "ddl": ddl,
        "graph": g,
    }


def load_all_schemas(tables_path: Path) -> dict[str, dict]:
    """Load tables.json and return dict of db_id → schema dict."""
    with open(tables_path, encoding="utf-8") as f:
        all_tables = json.load(f)
    return {db["db_id"]: build_db_schema(db) for db in all_tables}
