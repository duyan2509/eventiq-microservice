"""Stage 6 — execute generated SQL against the analytics DB.

`execute_sql` runs a single read-only statement and returns
(rows_as_dicts, error_or_None). The transaction is rolled back so a
failed query never leaves the connection in a poisoned state.

`execute_with_retry` is the LLM-aware variant. If the first execution
fails, it sends the Postgres error + naming/enum rules + the
subgraph DDL back to the LLM and asks for a corrected statement.
Capped at one retry.
"""
from __future__ import annotations

import psycopg2

from . import llm_client
from .db import connect
from .sql_generation import clean_sql


def _rows_as_dicts(cur) -> list[dict]:
    if cur.description is None:
        return []
    cols = [d.name for d in cur.description]
    return [dict(zip(cols, row)) for row in cur.fetchall()]


def execute_sql(sql: str) -> tuple[list[dict], str | None]:
    """Run `sql` and return (rows, error_message_or_None).

    Rows come back as dicts (column name → value). The connection is
    always closed by the context manager so callers don't need to
    worry about pooling.
    """
    try:
        with connect() as conn, conn.cursor() as cur:
            cur.execute(sql)
            rows = _rows_as_dicts(cur)
        return rows, None
    except psycopg2.Error as e:
        msg = (e.diag.message_primary or str(e)).strip() if e.diag else str(e).strip()
        return [], msg
    except Exception as e:
        return [], f"{type(e).__name__}: {e}"


_CORRECTION_RULES = (
    "NHẮC LẠI quy tắc:\n"
    "- user_service, org_service dùng PascalCase quote: \"Users\", \"Id\".\n"
    "- event_service, seat_service, payment_service dùng snake_case KHÔNG quote.\n"
    "- event_service.events.status là INT (0=Draft 1=Pending 2=Approved 3=Rejected 4=Published 5=Cancelled).\n"
    "- payment_service.orders.status là TEXT ('Pending'|'Paid'|'Failed'|'Refunded').\n"
    "- Cột audit chuẩn: created_at, updated_at, is_deleted (snake) / \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\" (Pascal).\n"
)


def _build_correction_prompt(
    sql: str, error: str, subgraph: dict, schema: dict[str, str]
) -> str:
    ddl = "\n\n".join(schema[t] for t in subgraph.get("tables", []) if t in schema)
    return (
        f"SQL sau bị lỗi PostgreSQL.\n\n"
        f"Lỗi: {error}\n\n"
        f"{_CORRECTION_RULES}\n"
        f"Schema (chỉ dùng các bảng/cột này):\n{ddl}\n\n"
        f"SQL sai:\n{sql}\n\n"
        f"Sửa lại, CHỈ trả về SQL hoàn chỉnh, KHÔNG markdown, KHÔNG giải thích:"
    )


def execute_with_retry(
    sql: str,
    subgraph: dict,
    schema: dict[str, str],
) -> tuple[list[dict], str | None, int, str]:
    """Execute `sql`. On Postgres error, retry once with LLM correction.

    Returns
    -------
    rows : list[dict]
    error : str | None       — last Postgres error (None on success).
    retries : int            — 0 or 1.
    final_sql : str          — the SQL that was actually executed last.
    """
    rows, error = execute_sql(sql)
    if error is None:
        return rows, None, 0, sql

    prompt = _build_correction_prompt(sql, error, subgraph, schema)
    raw = llm_client.call(prompt, max_tokens=600, temperature=0.0)
    corrected = clean_sql(raw)
    rows, error = execute_sql(corrected)
    return rows, error, 1, corrected
