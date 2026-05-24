"""Pipeline entry point — `run_pipeline(question, G, schema) → dict`.

Orchestrates the seven stages of the Text2SQL pipeline:

1. Entity extraction (LLM #1) + name normalization
2. Routing by confidence threshold
3. Schema linking — graph traversal OR keyword fallback
4. Prompt assembly
5. SQL generation (LLM #2)
6. Execute with self-correction (LLM #3, retry once)
7. Chart-type heuristic

The output dict carries every artifact downstream consumers (eval
script, demo API, error analyser) need without forcing them to
re-run any stage.
"""
from __future__ import annotations

from typing import Any

import networkx as nx

from .chart_picker import pick_chart_type
from .entity_extraction import extract_and_normalize
from .schema_linking import schema_link
from .sql_generation import generate_sql
from .sql_runner import execute_with_retry


def run_pipeline(
    question: str,
    g: nx.Graph,
    schema: dict[str, str],
) -> dict[str, Any]:
    entity = extract_and_normalize(question)
    link = schema_link(question, entity, g, schema)
    sql = generate_sql(question, link, schema)
    rows, error, retries, final_sql = execute_with_retry(sql, link, schema)
    chart = pick_chart_type(rows, question)

    return {
        "question": question,
        "predicted_sql": final_sql,
        "first_sql": sql,
        "result": rows,
        "error": error,
        "schema_linking_method": link["method"],
        "relevant_tables": link["tables"],
        "join_hints": link["join_hints"],
        "entity_confidence": entity["confidence"],
        "retries": retries,
        "chart_type": chart,
    }
