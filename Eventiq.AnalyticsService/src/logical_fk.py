"""Cross-service logical foreign keys.

Physical FK only exist within a single service DB. Logical FK declared
here let the schema graph find JOIN paths across services. Format:
    (table_a, col_a, table_b, col_b)
where PascalCase tables include the surrounding quotes in the FQ name.

Verified against entity code as of Phase 1.
"""

LOGICAL_FK: list[tuple[str, str, str, str]] = [
    # payment ↔ user (snapshot user_id in Order)
    ('payment_service.orders',          'user_id',         'user_service."Users"',         'Id'),

    # payment ↔ org
    ('payment_service.orders',          'org_id',          'org_service."Organizations"',  'Id'),

    # payment ↔ event (session snapshot)
    ('payment_service.orders',          'session_id',      'event_service.sessions',       'id'),

    # payment_item ↔ seat
    ('payment_service.order_items',     'seat_id',         'seat_service.seats',           'id'),

    # ticket ↔ order
    ('event_service.tickets',           'order_id',        'payment_service.orders',       'id'),
    # ticket ↔ session
    ('event_service.tickets',           'session_id',      'event_service.sessions',       'id'),
    # ticket ↔ seat
    ('event_service.tickets',           'seat_id',         'seat_service.seats',           'id'),

    # event ↔ org
    ('event_service.events',            'organization_id', 'org_service."Organizations"',  'Id'),

    # seat_map ↔ event / chart / session / org
    ('seat_service.seat_maps',          'chart_id',        'event_service.charts',         'id'),
    ('seat_service.seat_maps',          'event_id',        'event_service.events',         'id'),
    ('seat_service.seat_maps',          'session_id',      'event_service.sessions',       'id'),
    ('seat_service.seat_maps',          'organization_id', 'org_service."Organizations"',  'Id'),

    # seat ↔ legend (legend lives in event_service)
    ('seat_service.seats',              'legend_id',       'event_service.legends',        'id'),

    # member ↔ user
    ('org_service."Members"',           'UserId',          'user_service."Users"',         'Id'),

    # invitation ↔ user
    ('org_service."Invitations"',       'UserId',          'user_service."Users"',         'Id'),

    # user_role ↔ org (org-scoped role)
    ('user_service."UserRoles"',        'OrganizationId',  'org_service."Organizations"',  'Id'),

    # org payment info ↔ org
    ('event_service.org_payment_info',  'organization_id', 'org_service."Organizations"',  'Id'),

    # submission ↔ event
    ('event_service.submissions',       'event_id',        'event_service.events',         'id'),

    # payout log ↔ org
    ('org_service."PayoutLogs"',        'OrgId',           'org_service."Organizations"',  'Id'),
]
