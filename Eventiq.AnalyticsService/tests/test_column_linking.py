from __future__ import annotations

from src.schema_columns import columns_of, column_meta
from src.column_linking import link_columns
from src.prompt_builder import build_prompt
from src.schema_dump import SCHEMA


def _roles(table):
    return {name.strip('"').lower(): role for name, _t, role in column_meta()[table]}


# ----------------------------------------------------------- parser + roles
def test_parser_keeps_snake_and_pascal_tokens():
    assert "total_amount" in columns_of("payment_service.orders")
    assert '"Id"' in columns_of('user_service."Users"')
    assert '"Email"' in columns_of('user_service."Users"')


def test_role_derivation_orders():
    r = _roles("payment_service.orders")
    assert r["total_amount"] == "measure"
    assert r["platform_fee"] == "measure"
    assert r["paid_at"] == "temporal"
    assert r["status"] == "status_enum"
    assert r["user_id"] == "key"
    assert r["org_id"] == "key"
    assert r["event_name"] == "dimension"


def test_role_events_status_is_enum():
    assert _roles("event_service.events")["status"] == "status_enum"


# ----------------------------------------------------------- matcher
def test_measure_and_temporal_and_status_selected():
    link = {"tables": ["payment_service.orders"], "join_hints": []}
    entity = {"aggregation": "SUM", "filters": ["status='Paid'"]}
    out = link_columns("Doanh thu theo tháng năm nay", entity, link, graph=None)
    cols = out["columns"]
    assert "payment_service.orders.total_amount" in cols      # measure (SUM)
    assert "payment_service.orders.paid_at" in cols            # temporal ("tháng")
    assert "payment_service.orders.status" in cols            # status_enum (filter)
    assert out["covered"]


def test_group_by_dimension_name():
    link = {"tables": ["event_service.events"], "join_hints": []}
    entity = {"aggregation": "COUNT", "filters": []}
    out = link_columns("Top 5 sự kiện bán nhiều vé nhất", entity, link, graph=None)
    assert "event_service.events.name" in out["columns"]


def test_explicit_column_mention():
    link = {"tables": ["payment_service.orders"], "join_hints": []}
    out = link_columns("tổng platform_fee", {"filters": []}, link, graph=None)
    assert "payment_service.orders.platform_fee" in out["columns"]


def test_not_covered_triggers_fallback():
    link = {"tables": ["payment_service.orders"], "join_hints": []}
    out = link_columns("liệt kê đơn hàng", {"aggregation": None, "filters": []}, link, graph=None)
    assert out["covered"] is False
    assert out["columns"] == []


def test_pascal_measure_ref_quoted():
    # legends.price is an int measure (snake); Organizations has none — sanity on quoting path
    link = {"tables": ['user_service."Users"'], "join_hints": []}
    out = link_columns('khách hàng có "Username" nào', {"filters": []}, link, graph=None)
    assert 'user_service."Users"."Username"' in out["columns"]


# ----------------------------------------------------------- prompt wiring
def test_prompt_includes_column_section_only_when_given():
    sub = {"tables": ["payment_service.orders"], "join_hints": []}
    with_cols = build_prompt("doanh thu?", sub, SCHEMA,
                             columns=["payment_service.orders.total_amount"])
    without = build_prompt("doanh thu?", sub, SCHEMA)
    assert "CỘT LIÊN QUAN" in with_cols
    assert "payment_service.orders.total_amount" in with_cols
    assert "CỘT LIÊN QUAN" not in without
