from __future__ import annotations

from src.value_linking import link_values, _candidate_columns
from src.prompt_builder import build_prompt
from src.schema_dump import SCHEMA


def _fake_fetch(values_by_col: dict[tuple[str, str], list[str]]):
    """Build a fetch(fq_table, col, limit) that serves canned distinct values
    and honours the high-cardinality guard (return None when over limit)."""
    def fetch(fq_table, col, limit):
        vals = values_by_col.get((fq_table, col))
        if vals is None:
            return None
        return None if len(vals) > limit else vals
    return fetch


# ----------------------------------------------------------- candidates
def test_candidates_are_text_category_columns_only():
    cands = _candidate_columns(["event_service.legends"])
    cols = {c for _t, c in cands}
    assert "name" in cols          # text dimension → probed
    assert "color" not in cols     # skip-listed free text
    assert "price" not in cols     # numeric measure → never probed
    assert "id" not in cols        # key → never probed
    assert "created_at" not in cols  # temporal → never probed


def test_candidates_include_pascal_text_columns():
    cands = _candidate_columns(['user_service."Roles"'])
    assert ('user_service."Roles"', '"Name"') in cands


# ----------------------------------------------------------- matching
def test_matches_real_value_mentioned_in_question():
    fetch = _fake_fetch({
        ("event_service.legends", "name"): ["VIP", "Standard", "Economy"],
    })
    out = link_values("Tỉ lệ ghế VIP đã bán", {"tables": ["event_service.legends"]},
                      fetch=fetch)
    assert out["covered"]
    assert ("event_service.legends", "name", "VIP") in out["values"]
    # unmatched values are not injected
    assert all(v[2] != "Standard" for v in out["values"])


def test_pascal_role_value_match():
    fetch = _fake_fetch({
        ('user_service."Roles"', '"Name"'): ["Admin", "User", "Staff", "Organization"],
    })
    out = link_values("Có bao nhiêu Staff trong hệ thống",
                      {"tables": ['user_service."Roles"']}, fetch=fetch)
    assert ('user_service."Roles"', '"Name"', "Staff") in out["values"]


def test_no_match_is_not_covered():
    fetch = _fake_fetch({
        ("event_service.legends", "name"): ["VIP", "Standard"],
    })
    out = link_values("Tổng số ghế còn trống", {"tables": ["event_service.legends"]},
                      fetch=fetch)
    assert out["covered"] is False
    assert out["values"] == []


def test_high_cardinality_column_is_skipped():
    # 80 distinct event names → over the default max_distinct=50 guard → None
    fetch = _fake_fetch({
        ("event_service.events", "name"): [f"Event {i}" for i in range(80)],
    })
    out = link_values("doanh thu của Event 3", {"tables": ["event_service.events"]},
                      fetch=fetch)
    assert out["covered"] is False


def test_word_bounded_match_avoids_substring_false_positive():
    fetch = _fake_fetch({
        ("event_service.legends", "name"): ["VI"],   # must not match inside "VIP"
    })
    out = link_values("ghế VIP", {"tables": ["event_service.legends"]}, fetch=fetch)
    assert out["covered"] is False


def test_numeric_and_short_values_ignored():
    fetch = _fake_fetch({
        ("event_service.legends", "name"): ["1", "A"],
    })
    out = link_values("loại 1 hạng A", {"tables": ["event_service.legends"]}, fetch=fetch)
    assert out["covered"] is False


# ----------------------------------------------------------- prompt wiring
def test_prompt_includes_value_section_only_when_given():
    sub = {"tables": ["event_service.legends"], "join_hints": []}
    with_vals = build_prompt("ghế VIP?", sub, SCHEMA,
                             values=[("event_service.legends", "name", "VIP")])
    without = build_prompt("ghế VIP?", sub, SCHEMA)
    assert "GIÁ TRỊ THỰC TẾ TRONG DB" in with_vals
    assert "event_service.legends.name = 'VIP'" in with_vals
    assert "GIÁ TRỊ THỰC TẾ TRONG DB" not in without
