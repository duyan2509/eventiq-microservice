import os
from contextlib import contextmanager

import psycopg2
from dotenv import load_dotenv

load_dotenv()

BUSINESS_SCHEMAS = (
    "user_service",
    "org_service",
    "event_service",
    "seat_service",
    "payment_service",
)


def _conn_kwargs() -> dict:
    mode = os.getenv("ANALYTICS_MODE", "dev").lower()
    prefix = "NEON_DB_" if mode == "dev" else "ANALYTICS_DB_"
    return {
        "host": os.environ[f"{prefix}HOST"],
        "port": int(os.environ[f"{prefix}PORT"]),
        "dbname": os.environ[f"{prefix}NAME"],
        "user": os.environ[f"{prefix}USER"],
        "password": os.environ[f"{prefix}PASSWORD"],
        "sslmode": os.environ.get(f"{prefix}SSLMODE", "require"),
    }


@contextmanager
def connect():
    conn = psycopg2.connect(**_conn_kwargs())
    try:
        yield conn
    finally:
        conn.close()


def current_mode() -> str:
    return os.getenv("ANALYTICS_MODE", "dev").lower()
