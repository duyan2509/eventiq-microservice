"""LLM-based entity extraction for Spider (English questions).

Given an English question and the list of tables in the Spider database,
asks the LLM to identify relevant tables with a confidence score.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))
from src import llm_client
from eval.config import MODEL

_JSON_RE = re.compile(r"\{.*\}", re.DOTALL)


def extract(question: str, table_names: list[str], db_id: str) -> dict:
    """Call LLM to identify relevant tables for a Spider question.

    Returns dict with:
      tables: list[str]  — table names from table_names
      confidence: float  — 0.0-1.0
    """
    table_list = "\n".join(f"  - {t}" for t in table_names)

    prompt = f"""You are a database expert. Identify which tables are needed to answer the question.

Database: {db_id}
Available tables:
{table_list}

Question: "{question}"

Return ONLY valid JSON, no markdown, no explanation:
{{"tables": ["table1", "table2"], "confidence": 0.9}}

Rules:
- Use exact table names from the list above
- Include only tables needed to answer the question
- confidence 0.9+ if certain, 0.7-0.9 if likely, below 0.7 if unsure
- At most 5 tables"""

    try:
        raw = llm_client.call(prompt, model=MODEL, max_tokens=200, temperature=0.0)
        m = _JSON_RE.search(raw)
        if not m:
            return {"tables": [], "confidence": 0.0}
        data = json.loads(m.group())
        tables = [t for t in data.get("tables", []) if t in table_names]
        confidence = float(data.get("confidence", 0.0))
        return {"tables": tables, "confidence": confidence}
    except Exception:
        return {"tables": [], "confidence": 0.0}
