"""Aggregate Spider eval results and print a breakdown table.

Usage (from AnalyticsService/ root):
  python -m eval.spider_report
"""
from __future__ import annotations

import json
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))
from eval.config import RESULTS_FILE

DIFFICULTY_ORDER = ["easy", "medium", "hard", "extra", "unknown"]


def load_results(path: Path) -> list[dict]:
    results = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    results.append(json.loads(line))
                except Exception:
                    pass
    return results


def compute_stats(results: list[dict]) -> dict:
    by_diff: dict[str, list[bool]] = defaultdict(list)
    by_db: dict[str, list[bool]] = defaultdict(list)
    schema_method: dict[str, int] = defaultdict(int)
    correction_count = 0

    for r in results:
        diff = r.get("difficulty", "unknown")
        by_diff[diff].append(r["ex"])
        by_db[r["db_id"]].append(r["ex"])
        schema_method[r.get("schema_link_method", "unknown")] += 1
        if r.get("corrected"):
            correction_count += 1

    return {
        "by_difficulty": {d: by_diff[d] for d in DIFFICULTY_ORDER if d in by_diff},
        "by_db": dict(by_db),
        "schema_method": dict(schema_method),
        "correction_count": correction_count,
        "total": len(results),
    }


def print_report(results: list[dict]) -> None:
    stats = compute_stats(results)
    total = stats["total"]
    overall_correct = sum(r["ex"] for r in results)

    print(f"\n{'='*55}")
    print(f"  Spider Evaluation Results — {total} questions")
    print(f"{'='*55}")
    print(f"  Overall EX:  {overall_correct}/{total} = {overall_correct/total*100:.1f}%\n")

    print(f"  {'Difficulty':<10} {'Correct':>8} {'Total':>7} {'EX%':>7}")
    print(f"  {'-'*36}")
    for diff in DIFFICULTY_ORDER:
        if diff not in stats["by_difficulty"]:
            continue
        exs = stats["by_difficulty"][diff]
        c = sum(exs)
        n = len(exs)
        print(f"  {diff:<10} {c:>8} {n:>7} {c/n*100:>6.1f}%")

    print(f"\n  Schema linking method breakdown:")
    for method, count in sorted(stats["schema_method"].items()):
        pct = count / total * 100
        print(f"    {method:<20} {count:>5} ({pct:.1f}%)")

    corr = stats["correction_count"]
    print(f"\n  SQL corrections applied:  {corr} ({corr/total*100:.1f}%)")

    # Bottom 5 databases by accuracy
    db_stats = [
        (db, sum(exs), len(exs)) for db, exs in stats["by_db"].items() if len(exs) >= 3
    ]
    db_stats.sort(key=lambda x: x[1] / x[2])
    if db_stats:
        print(f"\n  Hardest databases (≥3 questions, by EX%):")
        for db, c, n in db_stats[:5]:
            print(f"    {db:<28} {c}/{n} = {c/n*100:.0f}%")

    print(f"\n  Results file: {RESULTS_FILE}")
    print(f"{'='*55}\n")


def main() -> None:
    if not RESULTS_FILE.exists():
        print(f"No results file found at {RESULTS_FILE}")
        print("Run `python -m eval.spider_eval` first.")
        sys.exit(1)

    results = load_results(RESULTS_FILE)
    if not results:
        print("Results file is empty.")
        sys.exit(1)

    print_report(results)


if __name__ == "__main__":
    main()
