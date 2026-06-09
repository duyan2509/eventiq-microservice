"""Org-scoped Text2SQL universe — the `org_analytics.*` views.

This module is the org-mode counterpart to `schema_dump` + `schema_graph`:
the LLM only ever sees these denormalised, pre-filtered views, never the raw
service schemas. Data isolation is enforced in the database (see
`scripts/setup_org_rls.sql`); this module just describes the views to the LLM
and wires up schema linking for them.

Keep the DDL strings here in sync with the views in `setup_org_rls.sql`.
"""
from __future__ import annotations

import networkx as nx

# DDL the LLM sees. Presented as views (read-only) so the model knows it may
# only SELECT. All snake_case, all in one schema, no quoting headaches.
ORG_SCHEMA: dict[str, str] = {
    "org_analytics.events": """\
CREATE VIEW org_analytics.events (  -- chỉ sự kiện của tổ chức hiện tại
  id          uuid,
  name        text,
  status      int,          -- 0=Draft 1=Pending 2=Approved 3=Rejected 4=Published 5=Cancelled
  start_time  timestamptz,
  end_time    timestamptz,
  created_at  timestamptz
);""",
    "org_analytics.sessions": """\
CREATE VIEW org_analytics.sessions (  -- suất diễn của sự kiện thuộc tổ chức
  id          uuid,
  name        text,
  start_time  timestamptz,
  end_time    timestamptz,
  event_id    uuid,
  event_name  text,         -- denormalised
  created_at  timestamptz
);""",
    "org_analytics.tickets": """\
CREATE VIEW org_analytics.tickets (  -- vé đã phát hành cho sự kiện của tổ chức
  id             uuid,
  order_id       uuid,
  session_id     uuid,
  seat_label     text,
  legend_name    text,
  price          numeric(18,2),
  is_checked_in  bool,
  checked_in_at  timestamptz,
  issued_at      timestamptz,
  event_id       uuid,
  event_name     text         -- denormalised
);""",
    "org_analytics.orders": """\
CREATE VIEW org_analytics.orders (  -- đơn hàng của tổ chức
  id            uuid,
  status        text,          -- 'Pending' | 'Paid' | 'Failed' | 'Refunded'
  total_amount  numeric(18,2),
  platform_fee  numeric(18,2),
  event_name    text,
  session_name  text,
  session_date  timestamptz,
  paid_at       timestamptz,
  created_at    timestamptz,
  session_id    uuid,
  user_id       uuid
);""",
    "org_analytics.order_items": """\
CREATE VIEW org_analytics.order_items (  -- dòng trong đơn hàng của tổ chức
  id           uuid,
  order_id     uuid,
  seat_label   text,
  legend_name  text,
  price        numeric(18,2),
  created_at   timestamptz
);""",
    "org_analytics.seat_maps": """\
CREATE VIEW org_analytics.seat_maps (  -- sơ đồ ghế của tổ chức
  id           uuid,
  event_id     uuid,
  name         text,
  status       text,          -- 'Draft' | 'Published' | 'Archived'
  version      int,
  total_seats  int,
  session_id   uuid,
  created_at   timestamptz
);""",
    "org_analytics.submissions": """\
CREATE VIEW org_analytics.submissions (  -- yêu cầu duyệt sự kiện của tổ chức
  id          uuid,
  event_id    uuid,
  status      int,          -- 0=Pending 1=Approved 2=Rejected 3=Withdrawn
  message     text,
  created_at  timestamptz,
  event_name  text
);""",
}

# Logical joins between the org views (no physical FKs on views). Columns are
# keyed by node name so JOIN-hint direction is path-order independent — same
# convention as `schema_graph`.
ORG_LOGICAL_FK: list[tuple[str, str, str, str]] = [
    ("org_analytics.orders",      "session_id", "org_analytics.sessions", "id"),
    ("org_analytics.sessions",    "event_id",   "org_analytics.events",   "id"),
    ("org_analytics.tickets",     "session_id", "org_analytics.sessions", "id"),
    ("org_analytics.tickets",     "order_id",   "org_analytics.orders",   "id"),
    ("org_analytics.order_items", "order_id",   "org_analytics.orders",   "id"),
    ("org_analytics.seat_maps",   "event_id",   "org_analytics.events",   "id"),
    ("org_analytics.submissions", "event_id",   "org_analytics.events",   "id"),
]


def build_org_graph() -> nx.Graph:
    """Schema graph over the org views (in-memory; no DB round-trip needed)."""
    g = nx.Graph()
    for table_a, col_a, table_b, col_b in ORG_LOGICAL_FK:
        g.add_node(table_a)
        g.add_node(table_b)
        g.add_edge(table_a, table_b, cols={table_a: col_a, table_b: col_b})
    return g
