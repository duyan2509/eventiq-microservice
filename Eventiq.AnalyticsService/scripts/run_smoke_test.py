"""Run scripts/smoke_test.sql against the configured DB (dev or prod)."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from src.db import connect, current_mode  # noqa: E402

SQL_FILE = Path(__file__).parent / "smoke_test.sql"


def main() -> int:
    print(f"[smoke] mode={current_mode()}")
    statements = [s.strip() for s in SQL_FILE.read_text(encoding="utf-8").split(";") if s.strip()]
    with connect() as conn, conn.cursor() as cur:
        for i, stmt in enumerate(statements, 1):
            print(f"\n--- Query {i} ---")
            cur.execute(stmt)
            if cur.description:
                cols = [d.name for d in cur.description]
                print(" | ".join(cols))
                for row in cur.fetchall():
                    print(" | ".join(str(v) for v in row))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
