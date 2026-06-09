from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path
from typing import Any, Callable

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from src.db import current_mode  # noqa: E402
from src.llm_client import DEFAULT_MODEL  # noqa: E402

DATASET_PATH = ROOT / "scripts" / "dataset.json"
CACHE_DIR = ROOT / "scripts" / ".eval_cache"
DIFFICULTIES = ["easy", "medium", "hard", "cross-service"]


def load_dataset() -> list[dict]:
    data = json.loads(DATASET_PATH.read_text(encoding="utf-8"))
    if not isinstance(data, list) or not data:
        raise SystemExit(f"dataset.json is empty or malformed: {DATASET_PATH}")
    return data


def guard_eval_mode() -> None:
    if current_mode() == "dev":
        raise SystemExit(
            "ANALYTICS_MODE=dev points at the LIVE app DB. Set "
            "ANALYTICS_MODE=eval (EVAL_DB_* in .env) before running eval, "
            "or pass --allow-dev if you really mean the dev DB."
        )


def build_graph():
    from src.schema_graph import build_graph_from_db
    return build_graph_from_db()


class SqlCache:
    def __init__(self, name: str, enabled: bool = True):
        CACHE_DIR.mkdir(exist_ok=True)
        self.path = CACHE_DIR / f"{name}.json"
        self.enabled = enabled
        self._data: dict[str, Any] = {}
        if enabled and self.path.exists():
            self._data = json.loads(self.path.read_text(encoding="utf-8"))

    @staticmethod
    def _key(variant: str, question: str) -> str:
        raw = f"{DEFAULT_MODEL}\x1f{variant}\x1f{question}"
        return hashlib.sha1(raw.encode("utf-8")).hexdigest()[:16]

    def get_or_compute(self, variant: str, question: str,
                       compute: Callable[[], Any]) -> tuple[Any, bool]:
        key = self._key(variant, question)
        if self.enabled and key in self._data:
            return self._data[key], True
        artifact = compute()
        if self.enabled:
            self._data[key] = artifact
            self.flush()
        return artifact, False

    def flush(self) -> None:
        self.path.write_text(
            json.dumps(self._data, ensure_ascii=False, indent=2, default=str),
            encoding="utf-8",
        )


def _acc(subset: list[dict]) -> str:
    if not subset:
        return "   —  "
    return f"{sum(r['correct'] for r in subset) / len(subset):6.1%}"


def print_breakdowns(results: list[dict]) -> None:
    n = len(results)
    overall = sum(r["correct"] for r in results) / n if n else 0.0
    print("\n" + "=" * 62)
    print(f"Overall EX Accuracy : {overall:.2%}  ({sum(r['correct'] for r in results)}/{n})")

    print("\nBy schema-linking method:")
    for method in ["graph", "keyword_fallback"]:
        sub = [r for r in results if r.get("schema_linking_method") == method]
        if sub:
            print(f"  {method:<17s}: {len(sub):>3d} câu ({len(sub)/n*100:4.1f}%) — "
                  f"EX Acc {_acc(sub)}")

    print("\nBy difficulty:")
    for diff in DIFFICULTIES:
        sub = [r for r in results if r.get("difficulty") == diff]
        print(f"  {diff:<14s}: {len(sub):>3d} câu — EX Acc {_acc(sub)}")

    print("\nBy #services involved:")
    for k in range(1, 6):
        sub = [r for r in results if len(r.get("involves_services", [])) == k]
        if sub:
            print(f"  {k} service(s)   : {len(sub):>3d} câu — EX Acc {_acc(sub)}")
    print("=" * 62)


def print_ablation_table(variants: dict[str, list[dict]]) -> None:
    print("\n" + "=" * 78)
    print("ABLATION — EX Accuracy by variant × difficulty")
    print("-" * 78)
    header = f"{'Variant':<28s}" + "".join(f"{d:>12s}" for d in DIFFICULTIES) + f"{'Overall':>12s}"
    print(header)
    print("-" * 78)
    for label, results in variants.items():
        cells = []
        for diff in DIFFICULTIES:
            sub = [r for r in results if r.get("difficulty") == diff]
            cells.append(_acc(sub))
        overall = _acc(results)
        print(f"{label:<28s}" + "".join(f"{c:>12s}" for c in cells) + f"{overall:>12s}")
    print("=" * 78)
