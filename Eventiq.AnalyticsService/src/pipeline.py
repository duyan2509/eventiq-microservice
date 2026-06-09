"""Pipeline entry point — `run_pipeline(question, G, schema) → dict`.

Orchestrates the seven stages of the Text2SQL pipeline:

1. Entity extraction (LLM #1) + name normalization
2. Routing by confidence threshold
3. Schema linking — graph traversal OR keyword fallback
3.5 Column-level linking — deterministic, by column role (default on)
3.6 Value-level linking — real DB cell values for mentioned literals (default on)
4. Prompt assembly
5. SQL generation (LLM #2)
6. Execute with self-correction (LLM #3, retry once)
7. Chart-type heuristic

Defaults enable column + value linking — the best configuration in the
ablation (see EVAL_REPORT.md).

The output dict carries every artifact downstream consumers (eval
script, demo API, error analyser) need without forcing them to
re-run any stage.
"""
from __future__ import annotations

from typing import Any

import networkx as nx

from .chart_picker import pick_chart
from .column_linking import link_columns
from .entity_extraction import extract_and_normalize
from .org_scope import ORG_SCHEMA
from .response_builder import generate_title
from .schema_linking import graph_traversal, keyword_matching, schema_link
from .sql_generation import generate_sql
from .sql_runner import execute_with_retry
from .value_linking import link_values


def run_pipeline(
    question: str,
    g: nx.Graph,
    schema: dict[str, str],
    *,
    use_column_linking: bool = True,
    use_value_linking: bool = True,
    enrich_prompt: bool = False,
) -> dict[str, Any]:
    entity = extract_and_normalize(question)
    link = schema_link(question, entity, g, schema)

    columns = None
    if use_column_linking:
        col = link_columns(question, entity, link, g)
        # Fall back to full DDL when nothing resolved, to avoid over-constraining.
        columns = col["columns"] if col["covered"] else None

    values = None
    if use_value_linking:
        val = link_values(question, link)
        values = val["values"] if val["covered"] else None

    sql = generate_sql(question, link, schema, columns=columns, values=values,
                       enrich=enrich_prompt)
    rows, error, retries, final_sql = execute_with_retry(sql, link, schema)
    chart_config = pick_chart(rows, question)

    return {
        "question": question,
        "predicted_sql": final_sql,
        "first_sql": sql,
        "result": rows,
        "error": error,
        "schema_linking_method": link["method"],
        "relevant_tables": link["tables"],
        "join_hints": link["join_hints"],
        "column_hints": columns,
        "value_hints": values,
        "entity_confidence": entity["confidence"],
        "retries": retries,
        "chart_type": chart_config["type"],
        "chart_config": chart_config,
        "title": generate_title(question),
    }


def run_pipeline_org(
    question: str,
    org_graph: nx.Graph,
    org_id: str,
) -> dict[str, Any]:
    """Org-scoped variant: the LLM only sees the `org_analytics` views and the
    query runs as the restricted role with `app.current_org` set to `org_id`.

    Schema linking goes straight to keyword matching over ORG_SCHEMA — the
    entity-extraction prompt is tuned for the raw 5-schema names, not these
    views — then enriches with graph JOIN hints when the picked views connect.
    """
    link = keyword_matching(question, ORG_SCHEMA)
    hints = graph_traversal(link["tables"], org_graph)
    link = {
        "tables": hints["tables"] or link["tables"],
        "join_hints": hints["join_hints"],
        "method": "org_scope",
    }
    sql = generate_sql(question, link, ORG_SCHEMA, org_mode=True)
    rows, error, retries, final_sql = execute_with_retry(
        sql, link, ORG_SCHEMA, scope="org", org_id=org_id, org_mode=True
    )
    chart_config = pick_chart(rows, question)

    return {
        "question": question,
        "predicted_sql": final_sql,
        "first_sql": sql,
        "result": rows,
        "error": error,
        "schema_linking_method": link["method"],
        "relevant_tables": link["tables"],
        "join_hints": link["join_hints"],
        "entity_confidence": 0.0,
        "retries": retries,
        "chart_type": chart_config["type"],
        "chart_config": chart_config,
        "title": generate_title(question),
    }
