"""Enum code → label maps, mirrored from the C# domain enums, plus a helper to
relabel integer enum codes in result rows so charts/tables show names, not codes.

Kept in sync manually with the backend domain model:
  - Eventiq.EventService/Domain/Entity/Event.cs → `enum EventStatus`

Both `events.status` and `submissions.status` are EventStatus — `Submission.Status`
is typed `EventStatus` in the domain model (despite an outdated comment in the
org_analytics view DDL). In the org views these are the ONLY integer status
columns; `orders.status` and `seat_maps.status` are already exposed as text.
"""
from __future__ import annotations

from typing import Any

# EventStatus (Event.cs): declaration order defines the integer code.
EVENT_STATUS: dict[int, str] = {
    0: "Draft",
    1: "Pending",
    2: "Approved",
    3: "Rejected",
    4: "Published",
    5: "Cancelled",
}

# Substring that marks a result column as carrying a status enum code.
_STATUS_HINT = "status"


def relabel_enum_values(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Map integer status codes to their EventStatus labels, in place.

    Only integer values in columns whose name contains 'status' are mapped.
    Text values (e.g. order status 'Paid') and non-status columns are left
    untouched, and a code outside the enum is kept as-is (defensive). Safe to
    call on the org-scoped result set, where the only integer status columns
    are events.status / submissions.status (both EventStatus).
    """
    if not rows:
        return rows
    status_cols = [c for c in rows[0].keys() if _STATUS_HINT in c.lower()]
    if not status_cols:
        return rows
    for row in rows:
        for c in status_cols:
            v = row.get(c)
            if isinstance(v, int) and not isinstance(v, bool) and v in EVENT_STATUS:
                row[c] = EVENT_STATUS[v]
    return rows
