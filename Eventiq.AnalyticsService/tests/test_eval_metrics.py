from __future__ import annotations

from decimal import Decimal

from src.eval_metrics import normalize, result_match


def test_empty_and_none_normalize_equal():
    assert normalize([]) == normalize(None) == []


def test_row_order_independent():
    a = [{"name": "A", "n": 1}, {"name": "B", "n": 2}]
    b = [{"name": "B", "n": 2}, {"name": "A", "n": 1}]
    assert result_match(a, b)


def test_column_order_independent():
    gold = [{"name": "Concert", "revenue": 1000}]
    pred = [{"revenue": 1000, "evt": "Concert"}]
    assert result_match(gold, pred)


def test_numeric_type_coercion():
    gold = [{"total": Decimal("1500000.00")}]
    pred = [{"total": 1500000.0}]
    assert result_match(gold, pred)
    assert result_match([{"x": Decimal("12.50")}], [{"x": 12.5}])


def test_distinct_results_do_not_match():
    assert not result_match([{"n": 1}], [{"n": 2}])
    assert not result_match([{"n": 1}], [{"n": 1}, {"n": 2}])


def test_none_value_vs_missing_row():
    assert not result_match([{"n": None}], [])
    assert result_match([{"n": None}], [{"n": None}])
