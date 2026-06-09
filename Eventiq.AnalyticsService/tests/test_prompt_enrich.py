from __future__ import annotations

from src.prompt_builder import build_prompt, _FEW_SHOT_PATTERNS, _QUERY_RULES
from src.schema_dump import SCHEMA

_SUB = {"tables": ["payment_service.orders"], "join_hints": []}


def test_baseline_prompt_unchanged_when_not_enriched():
    """V3 prompt must stay byte-identical so its cached results stay valid."""
    p = build_prompt("doanh thu?", _SUB, SCHEMA)
    assert "QUY TẮC TRUY VẤN" not in p
    assert "ROW_NUMBER()" not in p


def test_enrich_adds_rules_and_pattern_fewshot():
    p = build_prompt("doanh thu?", _SUB, SCHEMA, enrich=True)
    assert "QUY TẮC TRUY VẤN" in p
    assert "LEFT JOIN" in p
    assert "ROW_NUMBER()" in p
    assert "NOT EXISTS" in p
    assert "FILTER (WHERE" in p


def test_enrich_keeps_original_fewshot_too():
    p = build_prompt("doanh thu?", _SUB, SCHEMA, enrich=True)
    assert "Few-shot ví dụ" in p          # original block still present
    assert "Top 5 sự kiện" in p           # an original example


def test_rules_and_patterns_are_nonempty_constants():
    assert "LEFT JOIN" in _QUERY_RULES
    assert "PARTITION BY" in _FEW_SHOT_PATTERNS
