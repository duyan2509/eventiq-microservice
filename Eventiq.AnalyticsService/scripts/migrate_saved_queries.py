"""Create analytics_service.saved_queries table.

Run once before deploying the saved-queries endpoints:
    python -m scripts.migrate_saved_queries
"""
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from src.db import connect  # noqa: E402

_SQL = """
CREATE SCHEMA IF NOT EXISTS analytics_service;

CREATE TABLE IF NOT EXISTS analytics_service.saved_queries (
    id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id      uuid        NOT NULL,
    created_by  uuid        NOT NULL,
    title       text        NOT NULL,
    question    text        NOT NULL,
    sql         text        NOT NULL,
    pinned      bool        NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_saved_queries_org
    ON analytics_service.saved_queries (org_id)
    WHERE pinned = true;
"""

if __name__ == "__main__":
    with connect() as conn:
        cur = conn.cursor()
        cur.execute(_SQL)
        conn.commit()
    print("Migration complete: analytics_service.saved_queries created.")
