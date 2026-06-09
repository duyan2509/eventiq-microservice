"""Side-by-side gold-vs-predicted diff for an eval run.

Reads `eval_results_<variant>.json` (which already stores gold_sql +
predicted_sql), RE-EXECUTES both against the eval DB (no LLM calls) and
writes `diff_<variant>.json` with the actual row sets so value-level
differences are visible — not just row counts.

Usage:
  ANALYTICS_MODE=eval python scripts/diff_results.py                 # best config, wrong only
  ANALYTICS_MODE=eval python scripts/diff_results.py --variant full  # baseline
  ANALYTICS_MODE=eval python scripts/diff_results.py --all           # include correct ones too
"""
from __future__ import annotations

import argparse
import json

from _evalkit import ROOT, guard_eval_mode
from src.sql_runner import execute_sql


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--variant", default="full_col_val",
                    help="full | full_col | full_val | full_col_val | full_col_val_enr | ...")
    ap.add_argument("--all", action="store_true", help="include correct rows (default: wrong only)")
    ap.add_argument("--max-rows", type=int, default=30, help="rows kept per query in the diff")
    ap.add_argument("--allow-dev", action="store_true")
    args = ap.parse_args()

    if not args.allow_dev:
        guard_eval_mode()

    infile = ROOT / ("eval_results.json" if args.variant == "full"
                     else f"eval_results_{args.variant}.json")
    if not infile.exists():
        raise SystemExit(f"not found: {infile} (chạy eval.py cho variant này trước)")

    data = json.loads(infile.read_text(encoding="utf-8"))
    diffs = []
    for it in data:
        if not args.all and it.get("correct"):
            continue
        gold_rows, gold_err = execute_sql(it["gold_sql"])
        pred_sql = it.get("predicted_sql")
        pred_rows, pred_err = execute_sql(pred_sql) if pred_sql else ([], "no predicted_sql")
        diffs.append({
            "id": it["id"],
            "question": it["question"],
            "difficulty": it.get("difficulty"),
            "correct": it.get("correct"),
            "gold_sql": it["gold_sql"],
            "predicted_sql": pred_sql,
            "gold_row_count": len(gold_rows),
            "pred_row_count": len(pred_rows),
            "gold_error": gold_err,
            "pred_error": pred_err,
            "gold_rows": gold_rows[: args.max_rows],
            "pred_rows": pred_rows[: args.max_rows],
        })

    out = ROOT / f"diff_{args.variant}.json"
    out.write_text(json.dumps(diffs, ensure_ascii=False, indent=2, default=str),
                   encoding="utf-8")
    kind = "tất cả" if args.all else "câu SAI"
    print(f"Đã ghi {len(diffs)} {kind} → {out}")
    for d in diffs:
        flag = "OK " if d["correct"] else "ERR"
        print(f"  [{flag}] #{d['id']} gold={d['gold_row_count']:>3d} pred={d['pred_row_count']:>3d}  {d['question'][:55]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
