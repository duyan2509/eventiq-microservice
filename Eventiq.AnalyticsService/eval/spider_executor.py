"""SQLite execution and EX (execution accuracy) metric for Spider eval.

EX = 1 if the result set of the predicted SQL matches the gold SQL result set
(row-order independent, case-insensitive string normalization).
"""
from __future__ import annotations

import re
import sqlite3
import sys
from pathlib import Path
from typing import Optional

sys.path.insert(0, str(Path(__file__).parent.parent))
from src import llm_client
from eval.config import MODEL, SPIDER_DATABASES

_FENCE_RE = re.compile(r"```[\w]*\n?|```", re.IGNORECASE)


def get_db_path(db_id: str) -> Path:
    return SPIDER_DATABASES / db_id / f"{db_id}.sqlite"


def _run_sql(sql: str, db_id: str) -> tuple[Optional[list[tuple]], Optional[str]]:
    db_path = get_db_path(db_id)
    if not db_path.exists():
        return None, f"Database not found: {db_path}"
    try:
        conn = sqlite3.connect(str(db_path), timeout=30)
        conn.text_factory = lambda b: b.decode("utf-8", errors="replace")
        cursor = conn.cursor()
        cursor.execute(sql)
        rows = [tuple(r) for r in cursor.fetchall()]
        conn.close()
        return rows, None
    except Exception as e:
        return None, str(e)


def execute_sql(sql: str, db_id: str) -> Optional[list[tuple]]:
    rows, _ = _run_sql(sql, db_id)
    return rows


def execute_sql_with_error(sql: str, db_id: str) -> tuple[Optional[list[tuple]], Optional[str]]:
    return _run_sql(sql, db_id)


def compute_ex(
    pred_rows: Optional[list[tuple]],
    gold_rows: Optional[list[tuple]],
) -> bool:
    if pred_rows is None or gold_rows is None:
        return False

    def normalize(rows: list[tuple]) -> list[tuple]:
        normalized = []
        for row in rows:
            normalized.append(
                tuple(
                    str(v).strip().lower() if v is not None else ""
                    for v in row
                )
            )
        return sorted(normalized)

    return normalize(pred_rows) == normalize(gold_rows)


def clean_sql(raw: str) -> str:
    s = raw.strip()
    s = _FENCE_RE.sub("", s).strip()
    if ";" in s:
        s = s[: s.rindex(";") + 1]
    return s.rstrip(";") + ";"


def correct_sql(
    sql: str,
    error: str,
    question: str,
    ddl_str: str,
    db_id: str,
) -> tuple[Optional[str], Optional[list[tuple]]]:
    """Ask LLM to fix a failed SQL. Returns (corrected_sql, rows) or (None, None)."""
    prompt = f"""The following SQLite query failed with an error.

Error: {error}

Schema (SQLite):
{ddl_str}

Question: {question}

Failed SQL:
{sql}

Write the corrected SQLite query. Return ONLY the SQL ending with semicolon, no explanation, no markdown."""

    try:
        raw = llm_client.call(prompt, model=MODEL, max_tokens=400, temperature=0.0)
        corrected = clean_sql(raw)
        rows, _ = _run_sql(corrected, db_id)
        return corrected, rows
    except Exception:
        return None, None
