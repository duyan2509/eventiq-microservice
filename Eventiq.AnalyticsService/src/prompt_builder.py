"""Stage 4 — assemble the SQL-generation prompt.

The prompt has six sections, in order:

1. System instruction (terse, SQL-only, no markdown).
2. Naming convention rules (PascalCase quoting vs snake_case).
3. Enum value table (LLM forgets these otherwise).
4. DDL of the tables in the subgraph (verbatim from `schema_dump`).
5. JOIN hints from schema linking — direction-aware.
6. Few-shot examples covering the most common patterns.

Finally, the question is appended and the model is asked to emit SQL.
"""
from __future__ import annotations

from typing import Iterable

_SYSTEM = (
    "You are a PostgreSQL SQL expert. Return ONLY one complete SQL statement "
    "ending with a semicolon. NO explanations, NO markdown, NO ```sql wrapping."
)

_NAMING_RULES = """\
NAMING RULES (critical):
- Tables/columns in user_service and org_service: PascalCase, MUST quote with ".."
  e.g. user_service."Users"."Id", org_service."Organizations"."Name"
- Tables/columns in event_service, seat_service, payment_service: snake_case, NO quoting
  e.g. payment_service.orders.user_id, event_service.events.id
- NEVER use fdw_* prefix.
- NEVER use unqualified table names (e.g. bare `events` without schema prefix).
"""

_ENUMS = """\
ENUM VALUES:
- event_service.events.status (int):       0=Draft, 1=Pending, 2=Approved, 3=Rejected, 4=Published, 5=Cancelled
- event_service.submissions.status (int):  0=Pending, 1=Approved, 2=Rejected, 3=Withdrawn
- org_service."Organizations"."PaymentStatus" (int): 0=NotConfigured, 1=Pending, 2=Active, 3=Restricted
- org_service."Invitations"."Status" (int): 0=Pending, 1=Accepted, 2=Rejected, 3=Expired
- payment_service.orders.status (text):   'Pending' | 'Paid' | 'Failed' | 'Refunded'
- seat_service.seats.status (text):       'Available' | 'Holding' | 'Sold' | 'Blocked'
- seat_service.seat_maps.status (text):   'Draft' | 'Published' | 'Archived'
- user_service."Roles"."Name" (text):     'Admin' | 'User' | 'Staff' | 'Organization'
"""

_SOFT_DELETE = """\
SOFT DELETE: most tables have an is_deleted / "IsDeleted" column. When counting or \
aggregating active records, add a condition to exclude deleted rows (e.g. WHERE NOT is_deleted).\
"""

_ALIAS_RULES = """\
COLUMN ALIAS RULES (MANDATORY — the frontend charts by column name):
- EVERY column in SELECT must have a meaningful snake_case alias via `AS`, even direct columns.
  e.g. `e.name AS event_name`, `org."Name" AS org_name`.
- Computed/aggregate columns (COUNT, SUM, AVG, DATE_TRUNC, expressions) MUST have an alias.
  CORRECT: `COUNT(*) AS total_orders`, `SUM(o.total_amount) AS revenue`.
  WRONG:   `COUNT(*)`, `SUM(o.total_amount)` (Postgres returns `count` / `?column?`).
- Put label/category columns (name, status, month) BEFORE numeric columns — first column = X-axis/label.
- Alias should be a short, meaningful noun; no duplicates within the same query.
"""

_FEW_SHOT = """\
Few-shot examples:

-- Question: Monthly revenue?
SELECT DATE_TRUNC('month', o.paid_at) AS month,
       SUM(o.total_amount)            AS revenue
FROM payment_service.orders o
WHERE o.status = 'Paid'
GROUP BY 1
ORDER BY 1;

-- Question: Top 5 events by tickets sold?
SELECT e.name        AS event_name,
       COUNT(t.id)   AS tickets_sold
FROM event_service.events    e
JOIN event_service.sessions  s ON s.event_id   = e.id
JOIN event_service.tickets   t ON t.session_id = s.id
GROUP BY e.id, e.name
ORDER BY tickets_sold DESC
LIMIT 5;

-- Question: How many new users registered this month?
SELECT COUNT(*) AS new_users
FROM user_service."Users" u
WHERE u."CreatedAt" >= DATE_TRUNC('month', CURRENT_DATE)
  AND NOT u."IsDeleted";

-- Question: Which events were approved in the last week?
SELECT e.name       AS event_name,
       e.start_time AS start_time
FROM event_service.events e
WHERE e.status = 2                                         -- Approved
  AND e.updated_at >= CURRENT_DATE - INTERVAL '7 days';

-- Question: Which org had the highest revenue this quarter?
SELECT org."Name"          AS org_name,
       SUM(o.total_amount) AS revenue
FROM payment_service.orders o
JOIN org_service."Organizations" org ON org."Id" = o.org_id
WHERE o.status = 'Paid'
  AND o.paid_at >= DATE_TRUNC('quarter', CURRENT_DATE)
GROUP BY org."Id", org."Name"
ORDER BY revenue DESC
LIMIT 1;
"""


# Best-practice rules, added only under `enrich` (keeps the baseline prompt
# byte-identical). Domain-agnostic — not tuned to specific questions.
_QUERY_RULES = """\
QUERY BEST PRACTICES (apply when relevant):
- When aggregating PER group (per org, per event, per customer...): \
LEFT JOIN from the group table to the detail table to KEEP groups with 0 records \
(do NOT let INNER JOIN drop groups with count=0).
- SELECT only the columns the question needs; do NOT add extra descriptive columns.
- Count distinct objects: use COUNT(DISTINCT ...).
- Ratios/percentages: cast to numeric and guard against divide-by-zero, \
e.g. `x::numeric / NULLIF(y, 0)`.
"""

# Few-shot for SQL constructs (window, HAVING, NOT EXISTS, ratio). Scenarios
# are disjoint from the eval questions — they teach the pattern, not the test.
_FEW_SHOT_PATTERNS = """\

-- Question: Number each session by start time within each event?
SELECT s.event_id, s.id,
       ROW_NUMBER() OVER (PARTITION BY s.event_id ORDER BY s.start_time) AS seq
FROM event_service.sessions s;

-- Question: Organizations with more than 5 events?
SELECT e.organization_id, COUNT(*) AS event_count
FROM event_service.events e
WHERE NOT e.is_deleted
GROUP BY e.organization_id
HAVING COUNT(*) > 5;

-- Question: Which organizations have never created any event?
SELECT org."Id", org."Name"
FROM org_service."Organizations" org
WHERE NOT org."IsDeleted"
  AND NOT EXISTS (
    SELECT 1 FROM event_service.events e WHERE e.organization_id = org."Id"
  );

-- Question: What is the ratio of paid orders to total orders?
SELECT COUNT(*) FILTER (WHERE o.status = 'Paid')::numeric
       / NULLIF(COUNT(*), 0) AS paid_ratio
FROM payment_service.orders o;
"""

_ORG_SYSTEM = (
    "You are a PostgreSQL SQL expert. Return ONLY one complete SQL statement "
    "ending with a semicolon. NO explanations, NO markdown, NO ```sql wrapping.\n"
    "Data is ALREADY automatically scoped to the current organization — NEVER "
    "add an organization_id/org_id filter, and NEVER query any other schema."
)

_ORG_RULES = """\
RULES (critical):
- ONLY use the views in the org_analytics schema listed below, written in snake_case, NO quoting.
  e.g. org_analytics.orders, org_analytics.events.
- NEVER use other tables/schemas (event_service, payment_service, ...) — access will be denied.
- The views already exclude soft-deleted records; do NOT add is_deleted conditions.
"""

_ORG_ENUMS = """\
ENUM VALUES:
- org_analytics.events.status (int):      0=Draft, 1=Pending, 2=Approved, 3=Rejected, 4=Published, 5=Cancelled
- org_analytics.submissions.status (int): 0=Pending, 1=Approved, 2=Rejected, 3=Withdrawn
- org_analytics.orders.status (text):     'Pending' | 'Paid' | 'Failed' | 'Refunded'
- org_analytics.seat_maps.status (text):  'Draft' | 'Published' | 'Archived'
"""

_ORG_FEW_SHOT = """\
Few-shot examples:

-- Question: Monthly revenue?
SELECT DATE_TRUNC('month', o.paid_at) AS month,
       SUM(o.total_amount)            AS revenue
FROM org_analytics.orders o
WHERE o.status = 'Paid'
GROUP BY 1
ORDER BY 1;

-- Question: Top 5 events by tickets sold?
SELECT t.event_name AS event_name,
       COUNT(*)     AS tickets_sold
FROM org_analytics.tickets t
GROUP BY t.event_name
ORDER BY tickets_sold DESC
LIMIT 5;

-- Question: Event count by status?
SELECT e.status AS status,
       COUNT(*) AS total
FROM org_analytics.events e
GROUP BY e.status
ORDER BY total DESC;
"""


def _ddl_section(tables: Iterable[str], schema: dict[str, str]) -> str:
    blocks = []
    for fq in tables:
        ddl = schema.get(fq)
        if ddl:
            blocks.append(ddl)
    if not blocks:
        return "(no DDL available — infer from table/column names)"
    return "\n\n".join(blocks)


def _join_section(hints: Iterable[str]) -> str:
    hints = list(hints)
    if not hints:
        return "(no JOIN hints from graph — infer JOIN conditions)"
    return "\n".join(f"- {h}" for h in hints)


def _column_section(columns) -> str:
    cols = list(columns or [])
    if not cols:
        return ""
    listed = "\n".join(f"- {c}" for c in cols)
    return (
        "RELEVANT COLUMNS (column linking): restrict SELECT/WHERE/GROUP BY/ORDER BY to the "
        "columns below. JOIN key columns listed under JOIN section may also be used. "
        "Do NOT add extra descriptive columns beyond what the question requires.\n"
        f"{listed}\n\n"
    )


def _value_section(values) -> str:
    vals = list(values or [])
    if not vals:
        return ""
    listed = "\n".join(f"- {tbl}.{col} = '{val}'" for tbl, col, val in vals)
    return (
        "ACTUAL DB VALUES (value linking): the words in the question match EXACTLY with real "
        "values in the following columns (fetched directly from the DB). When filtering by "
        "these words, MUST use the exact column and value listed; do NOT guess enum codes.\n"
        f"{listed}\n\n"
    )


def build_prompt(
    question: str,
    subgraph: dict,
    schema: dict[str, str],
    columns=None,
    values=None,
    enrich: bool = False,
) -> str:
    """Render the full prompt for SQL generation.

    `subgraph` is the dict produced by `schema_linking.schema_link`,
    i.e. it must have `tables: list[str]` and `join_hints: list[str]`.
    `columns` (optional) is the validated `table.col` list from
    `column_linking.link_columns`; when present the model is told to
    restrict SELECT/WHERE to those columns.
    """
    ddl_block = _ddl_section(subgraph.get("tables", []), schema)
    join_block = _join_section(subgraph.get("join_hints", []))
    rules_block = f"{_QUERY_RULES}\n" if enrich else ""
    few_shot = _FEW_SHOT + (_FEW_SHOT_PATTERNS if enrich else "")

    return (
        f"{_SYSTEM}\n\n"
        f"{_NAMING_RULES}\n"
        f"{_ENUMS}\n"
        f"{_SOFT_DELETE}\n"
        f"{_ALIAS_RULES}\n"
        f"{rules_block}"
        f"Schema (use ONLY the tables/columns listed below):\n{ddl_block}\n\n"
        f"JOIN relationships (prefer these exact conditions):\n{join_block}\n\n"
        f"{_column_section(columns)}"
        f"{_value_section(values)}"
        f"{few_shot}\n"
        f"Question: {question}\n"
        f"SQL:"
    )


def build_org_prompt(
    question: str,
    subgraph: dict,
    schema: dict[str, str],
) -> str:
    """Render the SQL-generation prompt for ORG mode (org_analytics views).

    Same structure as `build_prompt` but with the org system instruction,
    org-only naming rules, org enums and org few-shot. Org scoping is enforced
    in the DB, so the model is told NOT to add any org filter.
    """
    ddl_block = _ddl_section(subgraph.get("tables", []), schema)
    join_block = _join_section(subgraph.get("join_hints", []))

    return (
        f"{_ORG_SYSTEM}\n\n"
        f"{_ORG_RULES}\n"
        f"{_ORG_ENUMS}\n"
        f"{_ALIAS_RULES}\n"
        f"Available views (use ONLY the views listed below):\n{ddl_block}\n\n"
        f"JOIN relationships (prefer these exact conditions):\n{join_block}\n\n"
        f"{_ORG_FEW_SHOT}\n"
        f"Question: {question}\n"
        f"SQL:"
    )
