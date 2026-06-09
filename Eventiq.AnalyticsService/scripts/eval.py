from __future__ import annotations

import argparse
import json

from _evalkit import (
    ROOT,
    SqlCache,
    build_graph,
    guard_eval_mode,
    load_dataset,
    print_breakdowns,
)

from src.pipeline import run_pipeline
from src.schema_dump import SCHEMA
from src.sql_runner import execute_sql
from src.eval_metrics import result_match

RESULTS_PATH = ROOT / "eval_results.json"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-cache", action="store_true")
    ap.add_argument("--allow-dev", action="store_true")
    ap.add_argument("--column-linking", action="store_true",
                    help="enable column-level schema linking (V4)")
    ap.add_argument("--value-linking", action="store_true",
                    help="enable value/cell linking (V6); usually combined with --column-linking")
    ap.add_argument("--enrich", action="store_true",
                    help="enable general prompt rules + pattern few-shot (disjoint from eval)")
    ap.add_argument("--ids", default="",
                    help="comma-separated question ids to run (subset eval, e.g. 12,20,21)")
    args = ap.parse_args()

    if not args.allow_dev:
        guard_eval_mode()

    dataset = load_dataset()
    if args.ids:
        wanted = {s.strip() for s in args.ids.split(",") if s.strip()}
        dataset = [d for d in dataset if str(d["id"]) in wanted]
    g = build_graph()
    cache = SqlCache("full_pipeline", enabled=not args.no_cache)
    variant = "full" + ("_col" if args.column_linking else "") \
        + ("_val" if args.value_linking else "") + ("_enr" if args.enrich else "")
    results_path = ROOT / f"eval_results_{variant}.json" if variant != "full" else RESULTS_PATH

    results: list[dict] = []
    print(f"Running EX-Acc eval ({variant}) over {len(dataset)} questions...\n")

    for item in dataset:
        qid, question = item["id"], item["question"]

        pred, cached = cache.get_or_compute(
            variant, question,
            lambda q=question: run_pipeline(q, g, SCHEMA,
                                            use_column_linking=args.column_linking,
                                            use_value_linking=args.value_linking,
                                            enrich_prompt=args.enrich),
        )

        gold_rows, gold_err = execute_sql(item["gold_sql"])
        pred_rows = pred.get("result") or []
        correct = (gold_err is None) and (pred.get("error") is None) and \
            result_match(gold_rows, pred_rows)

        results.append({
            **item,
            "predicted_sql": pred.get("predicted_sql"),
            "schema_linking_method": pred.get("schema_linking_method"),
            "relevant_tables": pred.get("relevant_tables"),
            "retries": pred.get("retries"),
            "pred_error": pred.get("error"),
            "gold_error": gold_err,
            "gold_row_count": len(gold_rows),
            "pred_row_count": len(pred_rows),
            "correct": correct,
        })

        flag = "OK " if correct else "ERR"
        c = "C" if cached else " "
        gold_warn = " ⚠GOLD-ERR" if gold_err else (" ⚠GOLD-0ROW" if not gold_rows else "")
        print(f"  [{flag}]{c} #{str(qid):>3s} {pred.get('schema_linking_method',''):<17s} "
              f"gold={len(gold_rows):>3d} pred={len(pred_rows):>3d}  "
              f"{question[:50]}{gold_warn}")

    results_path.write_text(
        json.dumps(results, ensure_ascii=False, indent=2, default=str),
        encoding="utf-8",
    )
    print_breakdowns(results)
    print(f"\nDetailed results: {results_path}")

    gold_problems = [r for r in results if r["gold_error"] or r["gold_row_count"] == 0]
    if gold_problems:
        print(f"\n⚠ {len(gold_problems)} gold SQL returned error/0-row — fix dataset before "
              f"trusting accuracy (run scripts/verify_dataset.py).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
