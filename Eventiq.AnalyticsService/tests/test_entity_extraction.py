"""Day 3 acceptance — normalize_table_name unit tests + live LLM smoke.

Live LLM tests are skipped when GROQ_API_KEY is unset so this file
still runs in CI without external dependencies.
"""
from __future__ import annotations

import os

import pytest

from src.entity_extraction import (
    CONFIDENCE_THRESHOLD,
    entity_extraction,
    extract_and_normalize,
    normalize_table_name,
)
from src.schema_dump import SCHEMA

ALL_TABLES = list(SCHEMA.keys())

skip_live = pytest.mark.skipif(
    not os.getenv("GROQ_API_KEY"), reason="GROQ_API_KEY not set"
)


# -------------------------------------------------------- normalize
@pytest.mark.parametrize(
    "raw, expected",
    [
        # exact passthrough
        ('user_service."Users"', 'user_service."Users"'),
        ("event_service.events", "event_service.events"),
        # case-insensitive, no quotes
        ("user_service.users", 'user_service."Users"'),
        ('USER_SERVICE."USERS"', 'user_service."Users"'),
        # suffix match — short name
        ("customers", 'user_service."Users"'),   # spec acceptance — fuzzy fallback
        ("users", 'user_service."Users"'),
        ("Users", 'user_service."Users"'),
        ("tickets", "event_service.tickets"),
        ("ticket", "event_service.tickets"),    # difflib
        ("orders", "payment_service.orders"),
        ("seats", "seat_service.seats"),
        ("Organizations", 'org_service."Organizations"'),
        ("organizations", 'org_service."Organizations"'),
        # fuzzy fallback
        ("sesion", "event_service.sessions"),
        # unknown — returns None
        ("nonexistent_table", None),
        ("", None),
    ],
)
def test_normalize_table_name(raw: str, expected: str | None) -> None:
    assert normalize_table_name(raw, ALL_TABLES) == expected


def test_confidence_threshold_value() -> None:
    assert CONFIDENCE_THRESHOLD == 0.7


# -------------------------------------------------------- live LLM
@skip_live
def test_entity_extraction_revenue_question() -> None:
    """Spec acceptance: 'Doanh thu theo tháng?' → tables contains orders."""
    out = entity_extraction("Doanh thu theo tháng?")
    assert "tables" in out
    assert "confidence" in out
    # Either raw or after normalization should surface orders.
    raw_names = " ".join(out["tables"]).lower()
    assert "orders" in raw_names or any(
        "payment" in t.lower() for t in out["tables"]
    )


@skip_live
def test_extract_and_normalize_revenue() -> None:
    out = extract_and_normalize("Doanh thu theo tháng năm nay?")
    assert "payment_service.orders" in out["normalized_tables"]
    assert out["confidence"] >= 0.0


@skip_live
def test_extract_and_normalize_top_events() -> None:
    out = extract_and_normalize("Top 5 sự kiện bán nhiều vé nhất?")
    norm = set(out["normalized_tables"])
    # At least one of the expected business tables should appear.
    expected = {
        "event_service.events",
        "event_service.tickets",
        "event_service.sessions",
    }
    assert expected & norm, f"none of {expected} in {norm}"
