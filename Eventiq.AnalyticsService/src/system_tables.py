"""MassTransit + EF metadata tables that must be excluded from
schema graph, entity extraction, and prompt context."""

SYSTEM_TABLES = frozenset({
    "InboxState",
    "OutboxState",
    "OutboxMessage",
    "__EFMigrationsHistory",
})


def is_business_table(table_fq: str) -> bool:
    """`table_fq` is `<schema>.<table>` or `<schema>."<Table>"`."""
    short = table_fq.split(".", 1)[1].strip('"')
    return short not in SYSTEM_TABLES
