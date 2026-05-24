"""DDL cache for ~25 business tables across the 5 service schemas.

Phase 2 prompt builder loads from here to assemble schema context for
the LLM. Format per table: a list of column descriptors with type, key
role (PK / FK), nullability, and inline notes for enums or quirks.

Excluded by design (see system_tables.py): MassTransit + EF metadata.
"""
from __future__ import annotations

# Each value is a multi-line CREATE TABLE-style block. Indentation
# matters only for readability — the LLM consumes verbatim.

SCHEMA: dict[str, str] = {
    # -----------------------------------------------------------------
    # user_service (PascalCase — quoting required in SQL)
    # -----------------------------------------------------------------
    'user_service."Users"': """\
CREATE TABLE user_service."Users" (
  "Id"           uuid         PRIMARY KEY,
  "Email"        text         NOT NULL,
  "Username"     text         NOT NULL,
  "IsBanned"     bool         NOT NULL DEFAULT false,
  "PasswordHash" text,
  "Avatar"       text,
  "CreatedAt"    timestamptz  NOT NULL,
  "UpdatedAt"    timestamptz,
  "DeletedAt"    timestamptz,
  "IsDeleted"    bool         NOT NULL DEFAULT false  -- soft delete
);""",

    'user_service."Roles"': """\
CREATE TABLE user_service."Roles" (
  "Id"   uuid PRIMARY KEY,
  "Name" text NOT NULL  -- Admin | User | Staff | Organization
);""",

    'user_service."UserRoles"': """\
CREATE TABLE user_service."UserRoles" (
  "Id"             uuid PRIMARY KEY,
  "UserId"         uuid NOT NULL,  -- FK -> user_service."Users"."Id"
  "RoleId"         uuid NOT NULL,  -- FK -> user_service."Roles"."Id"
  "OrganizationId" uuid            -- nullable: cross-org role
);""",

    'user_service."BanHistories"': """\
CREATE TABLE user_service."BanHistories" (
  "Id"          uuid PRIMARY KEY,
  "UserId"      uuid NOT NULL,  -- FK -> user_service."Users"."Id"
  "Reason"      text,
  "BannedById"  uuid NOT NULL
);""",

    'user_service."RefreshTokens"': """\
CREATE TABLE user_service."RefreshTokens" (
  "Id"             uuid        PRIMARY KEY,
  "Token"          text        NOT NULL,
  "Expires"        timestamptz NOT NULL,
  "UserId"         uuid        NOT NULL,  -- FK -> Users
  "OrganizationId" uuid,
  "OrgName"        text
);""",

    'user_service."PasswordResetTokens"': """\
CREATE TABLE user_service."PasswordResetTokens" (
  "Id"      uuid        PRIMARY KEY,
  "Token"   text        NOT NULL,
  "Expires" timestamptz NOT NULL,
  "UserId"  uuid        NOT NULL  -- FK -> Users
);""",

    # -----------------------------------------------------------------
    # org_service (PascalCase — quoting required)
    # -----------------------------------------------------------------
    'org_service."Organizations"': """\
CREATE TABLE org_service."Organizations" (
  "Id"                   uuid PRIMARY KEY,
  "Name"                 text NOT NULL,
  "Description"          text,
  "OwnerId"              uuid NOT NULL,
  "OwnerEmail"           text NOT NULL,
  "StripeAccountId"      text,
  "PaymentStatus"        text,  -- e.g. NotConfigured | Active | Restricted
  "PaymentConfiguredAt"  timestamptz
);""",

    'org_service."Members"': """\
CREATE TABLE org_service."Members" (
  "Id"             uuid PRIMARY KEY,
  "UserId"         uuid,           -- nullable: invitation accepted later
  "Email"          text NOT NULL,
  "OrganizationId" uuid NOT NULL,  -- FK -> Organizations
  "PermissionId"   uuid NOT NULL   -- FK -> Permissions
);""",

    'org_service."Permissions"': """\
CREATE TABLE org_service."Permissions" (
  "Id"             uuid PRIMARY KEY,
  "Name"           text NOT NULL,
  "OrganizationId" uuid NOT NULL,  -- FK -> Organizations
  "IsDesigner"     bool NOT NULL DEFAULT false
);""",

    'org_service."Invitations"': """\
CREATE TABLE org_service."Invitations" (
  "Id"             uuid        PRIMARY KEY,
  "OrganizationId" uuid        NOT NULL,
  "UserId"         uuid,        -- nullable until invitee accepts
  "UserEmail"      text        NOT NULL,
  "ExpiresAt"      timestamptz NOT NULL,
  "Status"         text        NOT NULL,  -- Pending | Accepted | Rejected | Expired
  "PermissionId"   uuid        NOT NULL
);""",

    'org_service."PlatformConfigs"': """\
CREATE TABLE org_service."PlatformConfigs" (
  "Id"                uuid         PRIMARY KEY,
  "CurrentFeeRate"    numeric      NOT NULL,
  "PendingFeeRate"    numeric,
  "EffectiveDate"     timestamptz  NOT NULL,
  "PayoutDayOfMonth"  int          NOT NULL,
  "UpdatedAt"         timestamptz  NOT NULL,
  "UpdatedBy"         uuid         NOT NULL
);""",

    'org_service."PayoutLogs"': """\
CREATE TABLE org_service."PayoutLogs" (
  "Id"              uuid        PRIMARY KEY,
  "OrgId"           uuid        NOT NULL,  -- FK -> Organizations
  "StripePayoutId"  text        NOT NULL,
  "Amount"          numeric     NOT NULL,
  "Currency"        text        NOT NULL,
  "TriggeredAt"     timestamptz NOT NULL
);""",

    # -----------------------------------------------------------------
    # event_service (snake_case — no quoting)
    # -----------------------------------------------------------------
    'event_service.events': """\
CREATE TABLE event_service.events (
  id                   uuid         PRIMARY KEY,
  organization_id      uuid         NOT NULL,
  organization_name    text         NOT NULL,  -- snapshot
  oranization_avatar   text,                    -- SIC: typo in code, keep as-is
  event_banner         text,
  name                 text         NOT NULL,
  description          text,
  detail_address       text,
  province_code        text,
  commune_code         text,
  province_name        text,
  commune_name         text,
  status               int          NOT NULL,  -- 0=Draft 1=Pending 2=Approved 3=Rejected 4=Published 5=Cancelled
  start_time           timestamptz  NOT NULL,
  end_time             timestamptz  NOT NULL
);""",

    'event_service.sessions': """\
CREATE TABLE event_service.sessions (
  id          uuid        PRIMARY KEY,
  name        text        NOT NULL,
  start_time  timestamptz NOT NULL,
  end_time    timestamptz NOT NULL,
  event_id    uuid        NOT NULL,  -- FK -> events
  chart_id    uuid        NOT NULL   -- FK -> charts
);""",

    'event_service.charts': """\
CREATE TABLE event_service.charts (
  id        uuid PRIMARY KEY,
  name      text NOT NULL,
  event_id  uuid NOT NULL  -- FK -> events
);""",

    'event_service.legends': """\
CREATE TABLE event_service.legends (
  id        uuid PRIMARY KEY,
  name      text NOT NULL,
  color     text NOT NULL,
  price     int  NOT NULL,  -- ⚠ int (not numeric); be careful when SUM-ing alongside orders.total_amount
  event_id  uuid NOT NULL   -- FK -> events
);""",

    'event_service.submissions': """\
CREATE TABLE event_service.submissions (
  id            uuid PRIMARY KEY,
  event_id      uuid NOT NULL,  -- FK -> events
  admin_email   text,
  admin_id      uuid,
  message       text,
  status        text  -- Pending | Approved | Rejected | Withdrawn
);""",

    'event_service.tickets': """\
CREATE TABLE event_service.tickets (
  id              uuid           PRIMARY KEY,
  order_id        uuid           NOT NULL,  -- LOGICAL FK -> payment_service.orders.id
  session_id      uuid           NOT NULL,  -- FK -> sessions
  seat_id         uuid           NOT NULL,  -- LOGICAL FK -> seat_service.seats.id
  seat_label      text           NOT NULL,
  legend_name     text           NOT NULL,
  price           numeric(18,2)  NOT NULL,
  qr_code         text           NOT NULL,
  is_checked_in   bool           NOT NULL DEFAULT false,
  checked_in_at   timestamptz,
  issued_at       timestamptz    NOT NULL
);""",

    'event_service.org_payment_info': """\
CREATE TABLE event_service.org_payment_info (
  organization_id    uuid         PRIMARY KEY,  -- LOGICAL FK -> org_service."Organizations"."Id"
  stripe_account_id  text         NOT NULL,
  is_active          bool         NOT NULL,
  updated_at         timestamptz  NOT NULL
);""",

    # -----------------------------------------------------------------
    # seat_service (snake_case)
    # -----------------------------------------------------------------
    'seat_service.seat_maps': """\
CREATE TABLE seat_service.seat_maps (
  id                uuid   PRIMARY KEY,
  chart_id          uuid   NOT NULL,
  event_id          uuid   NOT NULL,
  organization_id   uuid   NOT NULL,
  session_id        uuid,           -- nullable: NULL=template, NOT NULL=per-session clone
  name              text   NOT NULL,
  status            text   NOT NULL,  -- Draft | Published | Archived
  canvas_settings   jsonb,
  version           int    NOT NULL DEFAULT 1,
  total_seats       int    NOT NULL DEFAULT 0
);""",

    'seat_service.seats': """\
CREATE TABLE seat_service.seats (
  id                  uuid PRIMARY KEY,
  seat_map_id         uuid NOT NULL,  -- FK -> seat_maps
  label               text NOT NULL,
  seat_number         text NOT NULL,
  status              text NOT NULL,  -- Available | Holding | Sold | Blocked
  seat_type           int  NOT NULL,  -- 1..4
  position            jsonb,
  legend_id           uuid,           -- LOGICAL FK -> event_service.legends.id
  custom_properties   jsonb,
  held_by             uuid,
  held_until          timestamptz
);""",

    'seat_service.objects': """\
CREATE TABLE seat_service.objects (
  id           uuid PRIMARY KEY,
  seat_map_id  uuid NOT NULL,  -- FK -> seat_maps
  object_type  text NOT NULL,  -- Stage | Door | Decor | Label | ...
  label        text,
  geometry     jsonb NOT NULL,
  style        jsonb,
  z_index      int   NOT NULL DEFAULT 0
);""",

    'seat_service.versions': """\
CREATE TABLE seat_service.versions (
  id                  uuid   PRIMARY KEY,
  seat_map_id         uuid   NOT NULL,
  version_number      int    NOT NULL,
  snapshot            jsonb  NOT NULL,
  created_by          uuid   NOT NULL,
  change_description  text
);""",

    # -----------------------------------------------------------------
    # payment_service (snake_case)
    # -----------------------------------------------------------------
    'payment_service.orders': """\
CREATE TABLE payment_service.orders (
  id                  uuid           PRIMARY KEY,
  user_id             uuid           NOT NULL,  -- LOGICAL FK -> user_service."Users"."Id"
  org_id              uuid           NOT NULL,  -- LOGICAL FK -> org_service."Organizations"."Id"
  session_id          uuid           NOT NULL,  -- LOGICAL FK -> event_service.sessions.id
  stripe_session_id   text           NOT NULL,
  status              text           NOT NULL,  -- Pending | Paid | Failed | Refunded
  total_amount        numeric(18,2)  NOT NULL,
  platform_fee        numeric(18,2)  NOT NULL,
  event_name          text           NOT NULL,  -- snapshot
  session_name        text           NOT NULL,  -- snapshot
  session_date        timestamptz    NOT NULL,  -- snapshot
  paid_at             timestamptz
);""",

    'payment_service.order_items': """\
CREATE TABLE payment_service.order_items (
  id            uuid           PRIMARY KEY,
  order_id      uuid           NOT NULL,  -- FK -> orders
  seat_id       uuid           NOT NULL,  -- LOGICAL FK -> seat_service.seats.id
  seat_label    text           NOT NULL,
  legend_name   text           NOT NULL,
  price         numeric(18,2)  NOT NULL
);""",
}


def get_ddl(table_fq: str) -> str:
    return SCHEMA[table_fq]


def all_tables() -> list[str]:
    return list(SCHEMA.keys())
