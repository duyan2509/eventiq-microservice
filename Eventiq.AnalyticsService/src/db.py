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


_MODE_PREFIX = {
    "dev": "NEON_DB_",
    "eval": "EVAL_DB_",
    "prod": "ANALYTICS_DB_",
}


def _conn_kwargs(scope: str = "admin") -> dict:
    """Connection params for a (mode, scope).

    Mode picks the database (dev/eval/prod); scope picks the account: `admin`
    uses the full-access account (`*_USER`/`*_PASSWORD`), `org` uses the
    restricted `analytics_org_ro` role (`*_ORG_USER`/`*_ORG_PASSWORD`) which can
    only read the org_analytics views — same host/db, different credentials.
    """
    prefix = _MODE_PREFIX.get(current_mode(), "ANALYTICS_DB_")
    if scope == "org":
        user = os.environ[f"{prefix}ORG_USER"]
        password = os.environ[f"{prefix}ORG_PASSWORD"]
    else:
        user = os.environ[f"{prefix}USER"]
        password = os.environ[f"{prefix}PASSWORD"]
    return {
        "host": os.environ[f"{prefix}HOST"],
        "port": int(os.environ[f"{prefix}PORT"]),
        "dbname": os.environ[f"{prefix}NAME"],
        "user": user,
        "password": password,
        "sslmode": os.environ.get(f"{prefix}SSLMODE", "require"),
    }


@contextmanager
def connect(scope: str = "admin"):
    conn = psycopg2.connect(**_conn_kwargs(scope))
    try:
        yield conn
    finally:
        conn.close()


def current_mode() -> str:
    return os.getenv("ANALYTICS_MODE", "dev").lower()
