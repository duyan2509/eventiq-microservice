"""Single-question pipeline for Spider eval.

Flow:
  1. Entity extraction (LLM #1, English) — optional, skip with SKIP_ENTITY_LLM=1
  2. Schema linking — graph traversal if high confidence, else keyword matching
  3. SQL generation (LLM #2, English, SQLite)
  4. SQLite execution
  5. SQL correction (LLM #3) — only on execution error
  6. Gold SQL execution + EX metric
"""
from __future__ import annotations

import re
import sys
from pathlib import Path
from typing import Optional

import networkx as nx

sys.path.insert(0, str(Path(__file__).parent.parent))
from src import llm_client
from src.schema_linking import graph_traversal

from eval import spider_entity_extraction as ee
from eval import spider_executor as ex
from eval import spider_prompt_builder as pb
from eval.config import MODEL, CONFIDENCE_THRESHOLD, SKIP_ENTITY_LLM

_WORD_RE = re.compile(r"\w+")


def _keyword_match(question: str, ddl_map: dict[str, str], top_k: int = 6) -> dict:
    """Simple English keyword matching over Spider DDL."""
    q_words = set(_WORD_RE.findall(question.lower()))
    scored = []
    for table, ddl in ddl_map.items():
        ddl_words = set(_WORD_RE.findall(ddl.lower()))
        overlap = len(q_words & ddl_words)
        bonus = 3 if table.lower() in question.lower() else 0
        scored.append((table, overlap + bonus))
    scored.sort(key=lambda x: x[1], reverse=True)
    tables = [t for t, s in scored[:top_k] if s > 0]
    return {"tables": tables, "join_hints": [], "method": "keyword_fallback"}


def run(item: dict, schema_cache: dict[str, dict]) -> dict:
    """Run the full pipeline for one Spider question. Returns a result dict."""
    question: str = item["question"]
    db_id: str = item["db_id"]
    gold_sql: str = item["query"]
    difficulty: str = item.get("difficulty", "unknown")

    db_schema = schema_cache[db_id]
    ddl_map: dict[str, str] = db_schema["ddl"]
    graph: nx.Graph = db_schema["graph"]
    table_names: list[str] = db_schema["table_names"]
    ddl_str = "\n\n".join(ddl_map.values())

    # ── Step 1: Entity extraction ────────────────────────────────────────────
    entity_result: dict = {"tables": [], "confidence": 0.0}
    entity_method = "none"

    if not SKIP_ENTITY_LLM:
        try:
            entity_result = ee.extract(question, table_names, db_id)
            entity_method = "llm"
        except Exception:
            entity_method = "llm_failed"

    confidence = float(entity_result.get("confidence", 0.0))
    candidate_tables: list[str] = entity_result.get("tables", [])

    # ── Step 2: Schema linking ───────────────────────────────────────────────
    if confidence >= CONFIDENCE_THRESHOLD and candidate_tables:
        subgraph = graph_traversal(candidate_tables, graph)
        subgraph["method"] = "graph"
        if not subgraph["tables"]:
            subgraph = _keyword_match(question, ddl_map)
    else:
        subgraph = _keyword_match(question, ddl_map)

    linked_tables: list[str] = subgraph.get("tables", [])

    # ── Step 3: SQL generation ───────────────────────────────────────────────
    subgraph_ddl = {t: ddl_map[t] for t in linked_tables if t in ddl_map}
    prompt = pb.build_prompt(question, subgraph, subgraph_ddl)

    raw = llm_client.call(prompt, model=MODEL, max_tokens=400, temperature=0.0)
    pred_sql = ex.clean_sql(raw)

    # ── Step 4: Execute predicted SQL ────────────────────────────────────────
    pred_rows, error = ex.execute_sql_with_error(pred_sql, db_id)

    # ── Step 5: SQL correction on error ──────────────────────────────────────
    corrected_sql: Optional[str] = None
    correction_error: Optional[str] = None

    if error:
        corrected_sql, pred_rows = ex.correct_sql(pred_sql, error, question, ddl_str, db_id)
        if corrected_sql is None:
            correction_error = "correction_failed"

    # ── Step 6: Execute gold SQL + EX metric ─────────────────────────────────
    gold_rows = ex.execute_sql(gold_sql, db_id)
    is_ex = ex.compute_ex(pred_rows, gold_rows)

    return {
        "question": question,
        "db_id": db_id,
        "difficulty": difficulty,
        "gold_sql": gold_sql,
        "pred_sql": corrected_sql or pred_sql,
        "pred_sql_raw": pred_sql,
        "corrected": corrected_sql is not None,
        "ex": is_ex,
        "exec_error": error,
        "correction_error": correction_error,
        "entity_method": entity_method,
        "schema_link_method": subgraph.get("method"),
        "entity_tables": candidate_tables,
        "linked_tables": linked_tables,
    }
