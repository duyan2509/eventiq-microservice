"""MassTransit + EF metadata tables that must be excluded from
schema graph, entity extraction, and prompt context."""

SYSTEM_TABLES = frozenset({
    # PascalCase variants (user_service, org_service)
    "InboxState",
    "OutboxState",
    "OutboxMessage",
    "__EFMigrationsHistory",
    # snake_case variants (event_service, seat_service, payment_service)
    "inbox_state",
    "outbox_state",
    "outbox_message",
})


def is_business_table(table_fq: str) -> bool:
    """`table_fq` is `<schema>.<table>` or `<schema>."<Table>"`."""
    short = table_fq.split(".", 1)[1].strip('"')
    return short not in SYSTEM_TABLES
