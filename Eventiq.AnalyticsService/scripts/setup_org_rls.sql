-- ============================================================================
-- Org-scoped Text2SQL — DB-enforced data isolation (approach "A'")
-- ----------------------------------------------------------------------------
-- Why this exists:
--   Org users get FREE-FORM Text2SQL, but the LLM is NOT a security boundary.
--   We therefore enforce org scoping at the DATABASE layer, not in the prompt:
--
--     1. A dedicated, restricted role `analytics_org_ro` (NOT superuser, NOT a
--        table owner) — so Postgres privileges actually apply to it.
--     2. A schema `org_analytics` of security-barrier VIEWS. Each view joins
--        back to organization_id and filters by a per-request session GUC
--        `app.current_org`. The role is granted SELECT on these views ONLY —
--        it has NO access to the base service schemas. Even if the LLM emits
--        `SELECT * FROM event_service.events`, the role gets "permission denied".
--     3. The Python service sets `SET LOCAL app.current_org = '<orgId>'` (taken
--        from the signed JWT, never from the question) before each query.
--
--   Views are owned by the role running THIS script (must own the base tables,
--   e.g. neondb_owner) so they read base tables via the definer's rights while
--   the restricted role cannot touch those tables directly.
--
-- Run as the table owner (e.g. neondb_owner):
--   psql "$NEON_DB_URL" -v org_pw="'a-strong-password'" -f scripts/setup_org_rls.sql
-- (or paste into the Neon SQL editor, replacing :org_pw with a quoted literal)
-- ============================================================================

-- ---- 1. Restricted role -----------------------------------------------------
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'analytics_org_ro') THEN
    -- :org_pw must be a quoted literal, e.g. -v org_pw="'secret'"
    EXECUTE format('CREATE ROLE analytics_org_ro LOGIN PASSWORD %L '
                || 'NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT', :org_pw);
  END IF;
END $$;

GRANT CONNECT ON DATABASE neondb TO analytics_org_ro;   -- adjust DB name if not `neondb`

-- Defensive: ensure the role can never reach the raw service schemas.
REVOKE ALL ON SCHEMA
  event_service, payment_service, seat_service, org_service, user_service
  FROM analytics_org_ro;

-- ---- 2. Org-analytics schema + the session-org accessor ---------------------
CREATE SCHEMA IF NOT EXISTS org_analytics;
GRANT USAGE ON SCHEMA org_analytics TO analytics_org_ro;

-- Reads the per-request org from the session GUC. `true` = missing_ok, so an
-- unset GUC yields NULL → every view matches zero rows (fail-closed default).
CREATE OR REPLACE FUNCTION org_analytics.current_org() RETURNS uuid
  LANGUAGE sql STABLE AS
$$ SELECT NULLIF(current_setting('app.current_org', true), '')::uuid $$;

GRANT EXECUTE ON FUNCTION org_analytics.current_org() TO analytics_org_ro;

-- ---- 3. Org-scoped, snake_case, soft-delete-free views ----------------------
-- Every view: (a) is filtered to org_analytics.current_org(), (b) excludes
-- soft-deleted rows, (c) exposes only analytics-relevant columns, denormalised
-- so most questions need no joins.

CREATE OR REPLACE VIEW org_analytics.events WITH (security_barrier = true) AS
  SELECT e.id, e.name, e.status, e.start_time, e.end_time, e.created_at
  FROM event_service.events e
  WHERE e.organization_id = org_analytics.current_org()
    AND NOT e.is_deleted;

CREATE OR REPLACE VIEW org_analytics.sessions WITH (security_barrier = true) AS
  SELECT s.id, s.name, s.start_time, s.end_time, s.event_id,
         e.name AS event_name, s.created_at
  FROM event_service.sessions s
  JOIN event_service.events   e ON e.id = s.event_id
  WHERE e.organization_id = org_analytics.current_org()
    AND NOT s.is_deleted AND NOT e.is_deleted;

CREATE OR REPLACE VIEW org_analytics.tickets WITH (security_barrier = true) AS
  SELECT t.id, t.order_id, t.session_id, t.seat_label, t.legend_name, t.price,
         t.is_checked_in, t.checked_in_at, t.issued_at,
         s.event_id, e.name AS event_name
  FROM event_service.tickets  t
  JOIN event_service.sessions s ON s.id = t.session_id
  JOIN event_service.events   e ON e.id = s.event_id
  WHERE e.organization_id = org_analytics.current_org()
    AND NOT t.is_deleted;

CREATE OR REPLACE VIEW org_analytics.orders WITH (security_barrier = true) AS
  SELECT o.id, o.status, o.total_amount, o.platform_fee, o.event_name,
         o.session_name, o.session_date, o.paid_at, o.created_at,
         o.session_id, o.user_id
  FROM payment_service.orders o
  WHERE o.org_id = org_analytics.current_org()
    AND NOT o.is_deleted;

CREATE OR REPLACE VIEW org_analytics.order_items WITH (security_barrier = true) AS
  SELECT oi.id, oi.order_id, oi.seat_label, oi.legend_name, oi.price, oi.created_at
  FROM payment_service.order_items oi
  JOIN payment_service.orders      o ON o.id = oi.order_id
  WHERE o.org_id = org_analytics.current_org()
    AND NOT oi.is_deleted;

CREATE OR REPLACE VIEW org_analytics.seat_maps WITH (security_barrier = true) AS
  SELECT sm.id, sm.event_id, sm.name, sm.status, sm.version,
         sm.total_seats, sm.session_id, sm.created_at
  FROM seat_service.seat_maps sm
  WHERE sm.organization_id = org_analytics.current_org()
    AND NOT sm.is_deleted;

CREATE OR REPLACE VIEW org_analytics.submissions WITH (security_barrier = true) AS
  SELECT sub.id, sub.event_id, sub.status, sub.message, sub.created_at,
         e.name AS event_name
  FROM event_service.submissions sub
  JOIN event_service.events      e ON e.id = sub.event_id
  WHERE e.organization_id = org_analytics.current_org()
    AND NOT sub.is_deleted;

-- ---- 4. Grant SELECT on the views (and future ones) to the restricted role --
GRANT SELECT ON ALL TABLES IN SCHEMA org_analytics TO analytics_org_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA org_analytics
  GRANT SELECT ON TABLES TO analytics_org_ro;

-- ---- 5. Smoke test (optional) -----------------------------------------------
-- SET ROLE analytics_org_ro;
-- SET LOCAL app.current_org = '00000000-0000-0000-0000-000000000000';
-- SELECT count(*) FROM org_analytics.orders;          -- 0 rows for a bogus org
-- SELECT * FROM event_service.events LIMIT 1;          -- must ERROR: permission denied
-- RESET ROLE;
