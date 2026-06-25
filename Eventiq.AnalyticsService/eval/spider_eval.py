"""Main Spider evaluation runner.

Usage (from AnalyticsService/ root):
  python -m eval.spider_eval

Required env vars:
  GROQ_API_KEY=<together_api_key>
  LLM_BASE_URL=https://api.together.xyz/v1
  LLM_RATE_LIMIT_SLEEP=1.0

Optional:
  EVAL_MODEL=Qwen/Qwen2.5-Coder-32B-Instruct
  EVAL_MAX_QUESTIONS=100        # limit for quick smoke test
  EVAL_SKIP_ENTITY_LLM=0        # 1 = keyword matching only
"""
from __future__ import annotations

import json
import os
import sys
import time
from pathlib import Path

# Make src importable when run as `python -m eval.spider_eval` from AnalyticsService/
sys.path.insert(0, str(Path(__file__).parent.parent))

from dotenv import load_dotenv
load_dotenv()

from eval.config import (
    SPIDER_DEV,
    SPIDER_TABLES,
    RESULTS_DIR,
    RESULTS_FILE,
    MAX_QUESTIONS,
)
from eval import spider_schema as ss
from eval import spider_pipeline as pipeline


def _load_done_ids(results_file: Path) -> set[int]:
    """Read completed question indices from an existing JSONL for resume."""
    done: set[int] = set()
    if not results_file.exists():
        return done
    with open(results_file, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                obj = json.loads(line)
                done.add(obj["_idx"])
            except Exception:
                pass
    return done


def _append_result(results_file: Path, idx: int, result: dict) -> None:
    with open(results_file, "a", encoding="utf-8") as f:
        f.write(json.dumps({"_idx": idx, **result}) + "\n")


def _print_progress(idx: int, total: int, result: dict, elapsed: float) -> None:
    ex_mark = "✓" if result["ex"] else "✗"
    corr = " (corrected)" if result.get("corrected") else ""
    eta_s = int((elapsed / (idx + 1)) * (total - idx - 1))
    eta_str = f"{eta_s // 60}m{eta_s % 60:02d}s"
    question_preview = result["question"][:55]
    print(
        f"[{idx+1:4d}/{total}] {ex_mark}{corr} "
        f"db={result['db_id']:<20s} "
        f"eta={eta_str}  {question_preview}",
        flush=True,
    )


def main() -> None:
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)

    # Load Spider dev set
    if not SPIDER_DEV.exists():
        print(f"ERROR: Spider dev.json not found at {SPIDER_DEV}")
        print("Download Spider from https://yale-lily.github.io/spider and extract to eval/data/spider/")
        sys.exit(1)

    with open(SPIDER_DEV, encoding="utf-8") as f:
        dev_questions: list[dict] = json.load(f)

    if MAX_QUESTIONS > 0:
        dev_questions = dev_questions[:MAX_QUESTIONS]

    total = len(dev_questions)
    print(f"Spider dev set: {total} questions")

    # Load all Spider schemas (cached)
    print("Loading Spider schemas...", end=" ", flush=True)
    schema_cache = ss.load_all_schemas(SPIDER_TABLES)
    print(f"loaded {len(schema_cache)} databases")

    # Resume: skip already-processed questions
    done_ids = _load_done_ids(RESULTS_FILE)
    if done_ids:
        print(f"Resuming: {len(done_ids)} questions already done, skipping them")

    # Run eval
    start = time.monotonic()
    correct = sum(1 for line in open(RESULTS_FILE, encoding="utf-8") if '"ex": true' in line) if RESULTS_FILE.exists() else 0

    for idx, item in enumerate(dev_questions):
        if idx in done_ids:
            continue

        try:
            result = pipeline.run(item, schema_cache)
        except Exception as e:
            result = {
                "question": item.get("question", ""),
                "db_id": item.get("db_id", ""),
                "difficulty": item.get("difficulty", "unknown"),
                "gold_sql": item.get("query", ""),
                "pred_sql": "",
                "pred_sql_raw": "",
                "corrected": False,
                "ex": False,
                "exec_error": str(e),
                "correction_error": None,
                "entity_method": "error",
                "schema_link_method": None,
                "entity_tables": [],
                "linked_tables": [],
            }

        _append_result(RESULTS_FILE, idx, result)
        done_ids.add(idx)

        if result["ex"]:
            correct += 1

        elapsed = time.monotonic() - start
        _print_progress(idx, total, result, elapsed)

    # Final summary
    processed = len(done_ids)
    elapsed = time.monotonic() - start
    print(f"\n{'='*60}")
    print(f"Total: {processed} questions in {elapsed/60:.1f} min")
    print(f"EX (execution accuracy): {correct}/{processed} = {correct/processed*100:.1f}%")
    print(f"Results saved to: {RESULTS_FILE}")
    print("Run `python -m eval.spider_report` for breakdown by difficulty.")


if __name__ == "__main__":
    main()
