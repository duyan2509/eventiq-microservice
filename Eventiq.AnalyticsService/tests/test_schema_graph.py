"""Acceptance tests for Phase 1 — Day 2 schema graph.

Run with `pytest` from `Eventiq.AnalyticsService/`. No database needed
(physical FK loaded from tests/fixtures.py).
"""
from __future__ import annotations

import networkx as nx
import pytest

from src.logical_fk import LOGICAL_FK
from src.schema_dump import SCHEMA
from src.schema_graph import build_graph
from src.system_tables import SYSTEM_TABLES

from .fixtures import PHYSICAL_FK


@pytest.fixture(scope="module")
def graph() -> nx.Graph:
    return build_graph(PHYSICAL_FK, LOGICAL_FK)


# ---------------------------------------------------------------- nodes
def test_node_count_in_expected_range(graph: nx.Graph) -> None:
    # Spec target: ~25 nodes (±2). Floor 22 covers the case where a
    # table has no FK in either direction (e.g. PlatformConfigs).
    assert 22 <= graph.number_of_nodes() <= 27, sorted(graph.nodes())


def test_no_system_tables_in_graph(graph: nx.Graph) -> None:
    bad = [n for n in graph.nodes() if n.split(".", 1)[1].strip('"') in SYSTEM_TABLES]
    assert bad == [], f"system tables leaked into graph: {bad}"


def test_business_tables_appear(graph: nx.Graph) -> None:
    must_have = {
        'user_service."Users"',
        'org_service."Organizations"',
        'event_service.events',
        'event_service.sessions',
        'seat_service.seat_maps',
        'seat_service.seats',
        'payment_service.orders',
        'payment_service.order_items',
        'event_service.tickets',
    }
    missing = must_have - set(graph.nodes())
    assert not missing, f"missing: {missing}"


# ---------------------------------------------------------------- paths
def test_path_users_to_events(graph: nx.Graph) -> None:
    path = nx.shortest_path(
        graph, 'user_service."Users"', 'event_service.events'
    )
    assert path[0] == 'user_service."Users"'
    assert path[-1] == 'event_service.events'
    assert len(path) <= 5


def test_path_orders_to_seats(graph: nx.Graph) -> None:
    path = nx.shortest_path(
        graph, 'payment_service.orders', 'seat_service.seats'
    )
    assert path[0] == 'payment_service.orders'
    assert path[-1] == 'seat_service.seats'
    # Direct: orders -> order_items -> seats
    # or:    orders -> tickets -> seats
    assert len(path) == 3


def test_graph_connected(graph: nx.Graph) -> None:
    # All business tables (with FK) should sit in one component.
    components = list(nx.connected_components(graph))
    assert len(components) == 1, [sorted(c) for c in components]


# ---------------------------------------------------------------- edges
def test_edge_orders_users_attrs(graph: nx.Graph) -> None:
    attrs = graph['payment_service.orders']['user_service."Users"']
    # Edge is undirected; we don't enforce which endpoint is "a".
    assert {attrs["col_a"], attrs["col_b"]} == {"user_id", "Id"}


def test_edge_seats_legends_attrs(graph: nx.Graph) -> None:
    attrs = graph['seat_service.seats']['event_service.legends']
    assert {attrs["col_a"], attrs["col_b"]} == {"legend_id", "id"}


# ---------------------------------------------------------------- dump
def test_schema_dump_has_25_tables() -> None:
    assert len(SCHEMA) == 25, sorted(SCHEMA.keys())


def test_schema_dump_no_system_tables() -> None:
    leaked = [fq for fq in SCHEMA if fq.split(".", 1)[1].strip('"') in SYSTEM_TABLES]
    assert leaked == []


def test_schema_dump_covers_graph_nodes(graph: nx.Graph) -> None:
    # Every node in the graph must have a DDL entry — otherwise the
    # prompt builder cannot emit context for it.
    missing = set(graph.nodes()) - set(SCHEMA.keys())
    assert not missing, f"DDL missing for: {missing}"
