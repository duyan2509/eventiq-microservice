"""CRUD for analytics_service.saved_queries."""
from __future__ import annotations

import uuid

from .db import connect


def create(org_id: str, created_by: str, title: str, question: str, sql: str) -> dict:
    with connect() as conn:
        cur = conn.cursor()
        row_id = str(uuid.uuid4())
        cur.execute(
            """INSERT INTO analytics_service.saved_queries
               (id, org_id, created_by, title, question, sql)
               VALUES (%s, %s, %s, %s, %s, %s)
               RETURNING id, title, question, sql, created_at""",
            (row_id, org_id, created_by, title, question, sql),
        )
        row = cur.fetchone()
        conn.commit()
        cols = [d[0] for d in cur.description]
        return dict(zip(cols, row))


def list_for_org(org_id: str) -> list[dict]:
    with connect() as conn:
        cur = conn.cursor()
        cur.execute(
            """SELECT id, title, question, sql, created_at
               FROM analytics_service.saved_queries
               WHERE org_id = %s AND pinned = true
               ORDER BY created_at DESC""",
            (org_id,),
        )
        cols = [d[0] for d in cur.description]
        return [dict(zip(cols, r)) for r in cur.fetchall()]


def delete(query_id: str, org_id: str) -> bool:
    with connect() as conn:
        cur = conn.cursor()
        cur.execute(
            "DELETE FROM analytics_service.saved_queries WHERE id = %s AND org_id = %s",
            (query_id, org_id),
        )
        conn.commit()
        return cur.rowcount > 0
