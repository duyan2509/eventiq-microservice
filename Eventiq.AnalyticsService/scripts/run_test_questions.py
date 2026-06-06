"""Phase 2 acceptance — run all 20 questions through `run_pipeline` and
report:

  * crash count (must be 0)
  * SQL-execute success rate
  * expected-tables match rate
  * method breakdown (target ≥ 60% graph)
  * PascalCase quoting lint (must be 0 violations)

Run:
    python scripts/run_test_questions.py
"""
from __future__ import annotations

import json
import re
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from src.logical_fk import LOGICAL_FK            # noqa: E402
from src.pipeline import run_pipeline            # noqa: E402
from src.schema_dump import SCHEMA               # noqa: E402
from src.schema_graph import build_graph_from_db # noqa: E402


QUESTIONS_PATH = Path(__file__).resolve().parent / "eval_questions.json"

# Catch any PascalCase reference that wasn't quoted.
_PASCAL_UNQUOTED = re.compile(
    r'\b(user_service|org_service)\.([A-Z]\w*)\b(?!")'
)


def _tables_match(actual: list[str], expected: list[str]) -> bool:
    """Every expected table must be referenced in the pipeline output."""
    return all(t in actual for t in expected)


def main() -> int:
    questions = json.loads(QUESTIONS_PATH.read_text(encoding="utf-8"))
    g = build_graph_from_db()

    results: list[dict] = []
    crashes = 0

    print(f"Running {len(questions)} questions...\n")
    for q in questions:
        qid = q["id"]
        text = q["question"]
        expected_tables = q["expected_tables"]
        expected_method = q["expected_method"]
        expected_chart = q.get("expected_chart_type")

        try:
            out = run_pipeline(text, g, SCHEMA)
            crashed = False
        except Exception as e:
            crashes += 1
            crashed = True
            out = {
                "predicted_sql": "",
                "result": [],
                "error": f"CRASH {type(e).__name__}: {e}",
                "schema_linking_method": "crash",
                "relevant_tables": [],
                "retries": 0,
                "chart_type": "table",
            }

        sql = out["predicted_sql"]
        ran_ok = (not crashed) and out["error"] is None
        tables_ok = _tables_match(out["relevant_tables"], expected_tables)
        method_ok = out["schema_linking_method"] == expected_method
        # Only score chart type for questions that returned rows — an
        # empty result always falls back to "table" regardless of intent.
        chart_ok = (
            expected_chart is not None
            and len(out["result"]) > 0
            and out["chart_type"] == expected_chart
        )
        pascal_violations = _PASCAL_UNQUOTED.findall(sql)

        result = {
            "id": qid,
            "question": text,
            "difficulty": q["difficulty"],
            "method": out["schema_linking_method"],
            "expected_method": expected_method,
            "method_ok": method_ok,
            "tables_ok": tables_ok,
            "ran_ok": ran_ok,
            "rows": len(out["result"]),
            "retries": out["retries"],
            "chart": out["chart_type"],
            "expected_chart": expected_chart,
            "chart_ok": chart_ok,
            "pascal_violations": pascal_violations,
            "error": out["error"],
            "sql": sql,
            "relevant_tables": out["relevant_tables"],
        }
        results.append(result)

        status = "OK " if ran_ok else "ERR"
        tables_mark = "T" if tables_ok else "."
        method_mark = "M" if method_ok else "."
        chart_mark = "C" if chart_ok else "."
        pascal_mark = "!" if pascal_violations else " "
        print(
            f"  [{status}] #{qid:02d} {tables_mark}{method_mark}{chart_mark}{pascal_mark} "
            f"method={out['schema_linking_method']:<17s} rows={len(out['result']):>3d} "
            f"chart={out['chart_type']:<7s} retries={out['retries']}  {text[:50]}"
        )

    # ----- aggregate -----
    n = len(results)
    n_ran_ok = sum(r["ran_ok"] for r in results)
    n_tables_ok = sum(r["tables_ok"] for r in results)
    n_method_ok = sum(r["method_ok"] for r in results)
    # Chart accuracy is scored only over questions that returned rows.
    chart_scored = [r for r in results if r["rows"] > 0 and r["expected_chart"] is not None]
    n_chart_ok = sum(r["chart_ok"] for r in chart_scored)
    n_pascal_violations = sum(1 for r in results if r["pascal_violations"])
    method_counts = Counter(r["method"] for r in results)
    graph_pct = method_counts.get("graph", 0) / n * 100
    retry_total = sum(r["retries"] for r in results)

    print()
    print("=" * 60)
    print(f"Total questions      : {n}")
    print(f"Crashes              : {crashes}                {'PASS' if crashes == 0 else 'FAIL'}")
    print(f"Ran without DB error : {n_ran_ok}/{n}")
    print(f"Expected tables hit  : {n_tables_ok}/{n}      {'PASS' if n_tables_ok >= 14 else 'FAIL'} (target ≥14)")
    print(f"Expected method hit  : {n_method_ok}/{n}")
    if chart_scored:
        chart_pct = n_chart_ok / len(chart_scored) * 100
        print(f"Chart type hit       : {n_chart_ok}/{len(chart_scored)}      ({chart_pct:.1f}%, scored over rows>0)")
    print(f"Method breakdown     : {dict(method_counts)}")
    print(f"  graph %            : {graph_pct:.1f}%   {'PASS' if graph_pct >= 60 else 'FAIL'} (target ≥60%)")
    print(f"Self-correction used : {retry_total} times")
    print(f"PascalCase violations: {n_pascal_violations} questions    {'PASS' if n_pascal_violations == 0 else 'FAIL'}")
    print()

    # Write detailed report
    report_path = Path(__file__).resolve().parents[1] / "test_questions_report.json"
    report_path.write_text(json.dumps(results, indent=2, ensure_ascii=False, default=str),
                           encoding="utf-8")
    print(f"Detailed report: {report_path}")

    phase2_ok = (
        crashes == 0
        and n_tables_ok >= 14
        and graph_pct >= 60
        and n_pascal_violations == 0
    )
    print()
    print("Phase 2 acceptance:", "PASS" if phase2_ok else "REVIEW")
    return 0 if phase2_ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
