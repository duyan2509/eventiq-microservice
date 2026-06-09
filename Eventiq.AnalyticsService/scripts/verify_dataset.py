from __future__ import annotations

import argparse
import json
from collections import Counter

from _evalkit import DATASET_PATH, guard_eval_mode, load_dataset, DIFFICULTIES

from src.sql_runner import execute_sql


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true",
                    help="set verified=true on passing entries in dataset.json")
    ap.add_argument("--allow-dev", action="store_true")
    args = ap.parse_args()

    if not args.allow_dev:
        guard_eval_mode()

    dataset = load_dataset()
    ok = errors = empty = 0
    print(f"Verifying {len(dataset)} gold SQL statements...\n")

    for item in dataset:
        rows, err = execute_sql(item["gold_sql"])
        if err is not None:
            errors += 1
            item["verified"] = False
            print(f"  [ERR ] #{item['id']}  {item['question'][:46]}")
            print(f"         {err}")
        elif not rows:
            empty += 1
            item["verified"] = False
            print(f"  [0ROW] #{item['id']}  {item['question'][:46]}")
        else:
            ok += 1
            item["verified"] = True
            print(f"  [OK  ] #{item['id']}  rows={len(rows):<4d} {item['question'][:46]}")

    n = len(dataset)
    print("\n" + "=" * 60)
    print(f"Pass (>=1 row) : {ok}/{n}   {'PASS' if ok == n else 'FAIL'}")
    print(f"Postgres error : {errors}")
    print(f"Empty (0 row)  : {empty}")

    dist = Counter(i["difficulty"] for i in dataset)
    print("\nDifficulty distribution:")
    for d in DIFFICULTIES:
        print(f"  {d:<14s}: {dist.get(d, 0)}")
    for d in sorted(set(dist) - set(DIFFICULTIES)):
        print(f"  {d:<14s}: {dist[d]}  (⚠ unexpected label)")
    print("=" * 60)

    if args.write:
        DATASET_PATH.write_text(
            json.dumps(dataset, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        print(f"\nWrote verified flags back to {DATASET_PATH}")

    return 0 if ok == n else 1


if __name__ == "__main__":
    raise SystemExit(main())
