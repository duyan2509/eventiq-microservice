from __future__ import annotations

import argparse
import json

from _evalkit import (
    ROOT,
    SqlCache,
    build_graph,
    guard_eval_mode,
    load_dataset,
    print_ablation_table,
)

from src.column_linking import link_columns
from src.entity_extraction import extract_and_normalize
from src.schema_linking import keyword_matching, schema_link
from src.sql_generation import generate_sql, generate_sql_full_schema
from src.sql_runner import execute_with_retry
from src.schema_dump import SCHEMA
from src.sql_runner import execute_sql
from src.eval_metrics import result_match

RESULTS_PATH = ROOT / "ablation_results.json"

VARIANT_LABELS = {
    "no_linking": "V1 — No schema linking",
    "keyword_only": "V2 — Keyword matching only",
    "full": "V3 — Graph + fallback (table)",
    "full_col": "V4 — Graph + column linking",
}


def _gen_sql(variant: str, question: str, g) -> tuple[str, dict, str | None]:
    if variant == "no_linking":
        sql = generate_sql_full_schema(question, SCHEMA)
        return sql, {"tables": list(SCHEMA.keys()), "join_hints": []}, None
    if variant == "keyword_only":
        sub = keyword_matching(question, SCHEMA)
        return generate_sql(question, sub, SCHEMA), sub, "keyword_only"
    entity = extract_and_normalize(question)
    link = schema_link(question, entity, g, SCHEMA)
    if variant == "full_col":
        col = link_columns(question, entity, link, g)
        columns = col["columns"] if col["covered"] else None
        return generate_sql(question, link, SCHEMA, columns=columns), link, link["method"]
    return generate_sql(question, link, SCHEMA), link, link["method"]


def _make_artifact(variant: str, question: str, g) -> dict:
    sql, subgraph, method = _gen_sql(variant, question, g)
    return {"sql": sql, "subgraph": subgraph, "method": method}


def run_variant(variant: str, dataset: list[dict], g, cache: SqlCache,
                gold_cache: dict[str, list]) -> list[dict]:
    results = []
    print(f"\n=== {VARIANT_LABELS[variant]} ===")
    for item in dataset:
        question = item["question"]

        artifact, cached = cache.get_or_compute(
            variant, question,
            lambda v=variant, q=question: _make_artifact(v, q, g),
        )
        sql, subgraph, method = artifact["sql"], artifact["subgraph"], artifact["method"]

        rows, err, retries, final_sql = execute_with_retry(sql, subgraph, SCHEMA)

        if question not in gold_cache:
            gold_rows, _ = execute_sql(item["gold_sql"])
            gold_cache[question] = gold_rows
        gold_rows = gold_cache[question]

        correct = (err is None) and bool(gold_rows) and result_match(gold_rows, rows)
        results.append({
            "id": item["id"], "question": question,
            "difficulty": item["difficulty"],
            "involves_services": item.get("involves_services", []),
            "variant": variant,
            "schema_linking_method": method,
            "predicted_sql": final_sql, "error": err, "retries": retries,
            "row_count": len(rows), "correct": correct,
        })
        flag = "OK " if correct else "ERR"
        print(f"  [{flag}]{'C' if cached else ' '} #{str(item['id']):>3s} "
              f"rows={len(rows):>3d} {question[:50]}")
    return results


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-cache", action="store_true")
    ap.add_argument("--allow-dev", action="store_true")
    ap.add_argument("--only", default="no_linking,keyword_only,full,full_col",
                    help="comma-separated variants to run")
    args = ap.parse_args()

    if not args.allow_dev:
        guard_eval_mode()

    dataset = load_dataset()
    g = build_graph()
    cache = SqlCache("ablation_sql", enabled=not args.no_cache)
    gold_cache: dict[str, list] = {}

    variants = [v.strip() for v in args.only.split(",") if v.strip()]
    all_results: dict[str, list[dict]] = {}
    for v in variants:
        if v not in VARIANT_LABELS:
            raise SystemExit(f"unknown variant: {v}")
        all_results[VARIANT_LABELS[v]] = run_variant(v, dataset, g, cache, gold_cache)

    RESULTS_PATH.write_text(
        json.dumps({lbl: r for lbl, r in all_results.items()},
                   ensure_ascii=False, indent=2, default=str),
        encoding="utf-8",
    )

    print_ablation_table(all_results)

    full_label = VARIANT_LABELS["full"]
    if full_label in all_results:
        full = all_results[full_label]
        n = len(full)
        print("\nV3 method breakdown (graph vs keyword_fallback):")
        for method in ["graph", "keyword_fallback"]:
            sub = [r for r in full if r["schema_linking_method"] == method]
            if sub:
                acc = sum(r["correct"] for r in sub) / len(sub)
                print(f"  {method:<17s}: {len(sub):>3d} câu ({len(sub)/n*100:4.1f}%) — "
                      f"EX Acc {acc:.1%}")

    print(f"\nDetailed results: {RESULTS_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
