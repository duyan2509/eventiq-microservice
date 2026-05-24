"""Physical FK fixture — mirrors what `load_physical_fk(conn)` would
return against the live DB. Lets graph tests run without a database.

When migrations change physical FK, regenerate this list and update
the acceptance test ranges accordingly.
"""

PHYSICAL_FK: list[tuple[str, str, str, str]] = [
    # event_service
    ('event_service.sessions',                  'event_id',   'event_service.events',           'id'),
    ('event_service.sessions',                  'chart_id',   'event_service.charts',           'id'),
    ('event_service.legends',                   'event_id',   'event_service.events',           'id'),
    ('event_service.charts',                    'event_id',   'event_service.events',           'id'),

    # seat_service
    ('seat_service.seats',                      'seat_map_id', 'seat_service.seat_maps',        'id'),
    ('seat_service.objects',                    'seat_map_id', 'seat_service.seat_maps',        'id'),
    ('seat_service.versions',                   'seat_map_id', 'seat_service.seat_maps',        'id'),

    # payment_service
    ('payment_service.order_items',             'order_id',    'payment_service.orders',        'id'),

    # user_service
    ('user_service."UserRoles"',                'UserId',      'user_service."Users"',          'Id'),
    ('user_service."UserRoles"',                'RoleId',      'user_service."Roles"',          'Id'),
    ('user_service."BanHistories"',             'UserId',      'user_service."Users"',          'Id'),
    ('user_service."RefreshTokens"',            'UserId',      'user_service."Users"',          'Id'),
    ('user_service."PasswordResetTokens"',      'UserId',      'user_service."Users"',          'Id'),

    # org_service
    ('org_service."Members"',                   'OrganizationId', 'org_service."Organizations"', 'Id'),
    ('org_service."Members"',                   'PermissionId',   'org_service."Permissions"',   'Id'),
    ('org_service."Invitations"',               'OrganizationId', 'org_service."Organizations"', 'Id'),
    ('org_service."Invitations"',               'PermissionId',   'org_service."Permissions"',   'Id'),
    ('org_service."Permissions"',               'OrganizationId', 'org_service."Organizations"', 'Id'),
]
