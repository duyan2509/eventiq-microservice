-- =============================================================
-- Load Test Seed Data — Seat Designer / Booking Stress Test
-- =============================================================
-- Targets the single multi-schema PostgreSQL the services point at
-- (Neon for local dev, or Azure Flexible Server for the AKS deployment).
-- All EventIQ services share one database with per-service schemas
-- (user_service, event_service, seat_service, ...).
--
-- Run with (example, Neon dev):
--   psql "<connection-string>" \
--     -v load_test_user_id="'<UUID-of-loadtest-user>'" \
--     -f seed_load_test_data.sql
--
-- Optional override of the template seat count (default 2000):
--   ... -v seat_count=5000 -f seed_load_test_data.sql
--
-- STEP 0 (do this first, outside this script):
--   1. Start the services once (so UserService seeds roles).
--   2. Register the load test user:
--        curl -X POST <gateway>/auth/register \
--          -H "Content-Type: application/json" \
--          -d '{"email":"loadtest@eventiq.dev","password":"LoadTest@123","username":"loadtest"}'
--   3. Get the user UUID:
--        SELECT id FROM user_service.users WHERE email = 'loadtest@eventiq.dev';
--   4. Run this script with that UUID as load_test_user_id.
--
-- This script seeds EVERYTHING needed to run all k6 tests deterministically
-- (no need to drive the async approval/clone pipeline). It directly creates:
--   - a Published TEMPLATE seat map + N flat seats             (for design reads)
--   - a Published session CLONE of it + the same N seats       -> SESSION_ID (01, 04)
--   - an empty DRAFT seat map owned by the org                 -> SEAT_MAP_ID (03)
-- To instead exercise the real approval pipeline, see infrastructure.md §8.
-- =============================================================

-- Fixed IDs — keep stable so the k6 config/scripts can hardcode them
\set load_test_org_id        'a0000000-0000-0000-0000-000000000001'
\set load_test_event_id      'b0000000-0000-0000-0000-000000000001'
\set chart_pub_id            'c0000000-0000-0000-0000-0000000000aa'  -- chart for the published map
\set chart_draft_id          'c0000000-0000-0000-0000-0000000000bb'  -- chart for the draft map
\set session_pub_id          'e0000000-0000-0000-0000-0000000000aa'  -- SESSION_ID for 01 + 04
\set template_map_id         'd0000000-0000-0000-0000-0000000000aa'  -- Published template (session_id NULL)
\set clone_map_id            'd0000000-0000-0000-0000-0000000000bb'  -- Published clone (session_pub)
\set draft_map_id            'd0000000-0000-0000-0000-0000000000cc'  -- SEAT_MAP_ID for 03 (Draft)
-- :load_test_user_id   passed via -v on the command line (see STEP 0)
-- :seat_count          optional template seat count, defaults to 2000 below

-- Default seat_count to 2000 if not provided via -v seat_count=NNNN
\if :{?seat_count}
\else
  \set seat_count 2000
\endif

-- =============================================================
-- 1. USER SERVICE — assign Organization role + org membership
-- =============================================================
DO $$
DECLARE
  v_org_role_id uuid;
BEGIN
  SELECT id INTO v_org_role_id
  FROM user_service.roles
  WHERE name = 'Organization'
  LIMIT 1;

  IF v_org_role_id IS NULL THEN
    RAISE EXCEPTION 'Organization role not found — start UserService once first to seed roles.';
  END IF;

  INSERT INTO user_service.user_roles
    (id, user_id, role_id, organization_id, created_at, updated_at, is_deleted)
  VALUES
    (gen_random_uuid(), :'load_test_user_id'::uuid, v_org_role_id,
     :'load_test_org_id'::uuid, NOW(), NOW(), false)
  ON CONFLICT DO NOTHING;
END $$;

-- =============================================================
-- 2. EVENT SERVICE — org payment, event (Approved), charts + sessions
-- =============================================================

-- 2a. org_payment_infos — simulate Stripe-connected org
INSERT INTO event_service.org_payment_infos
  (id, organization_id, stripe_account_id, is_active, updated_at, created_at, is_deleted)
VALUES
  (gen_random_uuid(), :'load_test_org_id'::uuid, 'acct_loadtest_fake', true, NOW(), NOW(), false)
ON CONFLICT (organization_id) DO UPDATE
  SET is_active = true, stripe_account_id = 'acct_loadtest_fake';

-- 2b. event — status stored as integer by Dapper (Approved = 2)
INSERT INTO event_service.events
  (id, organization_id, organization_name, name, description,
   detail_address, province_code, commune_code, province_name, commune_name,
   status, start_time, end_time, created_at, updated_at, is_deleted)
VALUES
  (:'load_test_event_id'::uuid, :'load_test_org_id'::uuid, 'Load Test Org',
   '[LOAD TEST] Seat Designer Stress Event',
   'Auto-seeded event for k6 seat designer/booking load test — not for production.',
   '123 Test Street', 'VN-SG', '760', 'Ho Chi Minh City', 'District 1',
   2, '2026-07-01 08:00:00+00', '2026-07-01 22:00:00+00', NOW(), NOW(), false)
ON CONFLICT (id) DO NOTHING;

-- 2c. Fixed charts for the published + draft seat maps
INSERT INTO event_service.charts (id, name, event_id, created_at, updated_at, is_deleted)
VALUES
  (:'chart_pub_id'::uuid,   'Load Test Chart (published)', :'load_test_event_id'::uuid, NOW(), NOW(), false),
  (:'chart_draft_id'::uuid, 'Load Test Chart (draft)',     :'load_test_event_id'::uuid, NOW(), NOW(), false)
ON CONFLICT (id) DO NOTHING;

-- 2d. Fixed session for the published seat map (chart_pub) -> SESSION_ID
INSERT INTO event_service.sessions
  (id, event_id, name, start_time, end_time, chart_id, created_at, updated_at, is_deleted)
VALUES
  (:'session_pub_id'::uuid, :'load_test_event_id'::uuid, 'Load Test Session (published)',
   '2026-07-01 08:00:00+00', '2026-07-01 09:00:00+00', :'chart_pub_id'::uuid, NOW(), NOW(), false)
ON CONFLICT (id) DO NOTHING;

-- 2e. 600 extra charts + sessions (for 02-seat-api list/create load; one per VU slot)
INSERT INTO event_service.charts (id, name, event_id, created_at, updated_at, is_deleted)
SELECT gen_random_uuid(), 'Load Test Chart ' || i, :'load_test_event_id'::uuid, NOW(), NOW(), false
FROM generate_series(1, 600) AS i;

INSERT INTO event_service.sessions
  (id, event_id, name, start_time, end_time, chart_id, created_at, updated_at, is_deleted)
SELECT
  gen_random_uuid(), :'load_test_event_id'::uuid, 'Session ' || rn,
  '2026-07-01 10:00:00+00'::timestamptz + ((rn - 1) * interval '31 minutes'),
  '2026-07-01 10:00:00+00'::timestamptz + ((rn - 1) * interval '31 minutes') + interval '30 minutes',
  c.id, NOW(), NOW(), false
FROM (
  SELECT id, ROW_NUMBER() OVER (ORDER BY created_at, id) AS rn
  FROM event_service.charts
  WHERE event_id = :'load_test_event_id'::uuid AND is_deleted = false
    AND id NOT IN (:'chart_pub_id'::uuid, :'chart_draft_id'::uuid)
) c;

-- =============================================================
-- 3. SEAT SERVICE — template + session clone (with N seats) + draft map
-- =============================================================
-- position_x / position_y are STORED GENERATED columns derived from `position`
-- JSONB, so they are NOT inserted here — Postgres computes them automatically.

-- 3a. Published TEMPLATE seat map (session_id NULL) on chart_pub
INSERT INTO seat_service.seat_maps
  (id, chart_id, event_id, organization_id, session_id, name, status,
   canvas_settings, version, total_seats, created_at, updated_at, is_deleted)
VALUES
  (:'template_map_id'::uuid, :'chart_pub_id'::uuid, :'load_test_event_id'::uuid,
   :'load_test_org_id'::uuid, NULL, 'LT Template', 'Published',
   NULL, 1, :seat_count, NOW(), NULL, false)
ON CONFLICT (id) DO NOTHING;

-- 3b. Published CLONE for the fixed session (session_id = session_pub) on chart_pub
INSERT INTO seat_service.seat_maps
  (id, chart_id, event_id, organization_id, session_id, name, status,
   canvas_settings, version, total_seats, created_at, updated_at, is_deleted)
VALUES
  (:'clone_map_id'::uuid, :'chart_pub_id'::uuid, :'load_test_event_id'::uuid,
   :'load_test_org_id'::uuid, :'session_pub_id'::uuid, 'LT Template', 'Published',
   NULL, 1, :seat_count, NOW(), NULL, false)
ON CONFLICT (id) DO NOTHING;

-- 3c. Empty DRAFT seat map (session_id NULL) on chart_draft -> SEAT_MAP_ID for 03
INSERT INTO seat_service.seat_maps
  (id, chart_id, event_id, organization_id, session_id, name, status,
   canvas_settings, version, total_seats, created_at, updated_at, is_deleted)
VALUES
  (:'draft_map_id'::uuid, :'chart_draft_id'::uuid, :'load_test_event_id'::uuid,
   :'load_test_org_id'::uuid, NULL, 'LT Draft (SignalR)', 'Draft',
   NULL, 1, 0, NOW(), NULL, false)
ON CONFLICT (id) DO NOTHING;

-- 3d. Seats for template + clone — a square grid so the map has real extent.
--     Grid: cols = ceil(sqrt(N)), spacing 30px. Labels S1..SN (unique per map).
INSERT INTO seat_service.seats
  (id, seat_map_id, label, seat_number, status, seat_type, position,
   legend_id, custom_properties, held_by, held_until, created_at, updated_at, is_deleted)
SELECT
  gen_random_uuid(),
  m.map_id,
  'S' || i,
  i,
  'Available',
  1,
  jsonb_build_object(
    'x', ((i - 1) % cols) * 30,
    'y', ((i - 1) / cols) * 30
  ),
  NULL, NULL, NULL, NULL, NOW(), NULL, false
FROM
  (SELECT ceil(sqrt(:seat_count::numeric))::int AS cols) g,
  generate_series(1, :seat_count) AS i,
  (VALUES (:'template_map_id'::uuid), (:'clone_map_id'::uuid)) AS m(map_id)
ON CONFLICT DO NOTHING;

-- =============================================================
-- 4. VERIFY
-- =============================================================
SELECT 'user_roles'         AS tbl, COUNT(*) FROM user_service.user_roles
  WHERE user_id = :'load_test_user_id'::uuid
UNION ALL SELECT 'org_payment_infos', COUNT(*) FROM event_service.org_payment_infos
  WHERE organization_id = :'load_test_org_id'::uuid
UNION ALL SELECT 'events',   COUNT(*) FROM event_service.events
  WHERE id = :'load_test_event_id'::uuid
UNION ALL SELECT 'charts',   COUNT(*) FROM event_service.charts
  WHERE event_id = :'load_test_event_id'::uuid AND is_deleted = false
UNION ALL SELECT 'sessions', COUNT(*) FROM event_service.sessions
  WHERE event_id = :'load_test_event_id'::uuid AND is_deleted = false
UNION ALL SELECT 'seat_maps', COUNT(*) FROM seat_service.seat_maps
  WHERE event_id = :'load_test_event_id'::uuid AND is_deleted = false
UNION ALL SELECT 'seats (template)', COUNT(*) FROM seat_service.seats
  WHERE seat_map_id = :'template_map_id'::uuid AND is_deleted = false
UNION ALL SELECT 'seats (clone)', COUNT(*) FROM seat_service.seats
  WHERE seat_map_id = :'clone_map_id'::uuid AND is_deleted = false
UNION ALL SELECT 'position_x backfilled', COUNT(*) FROM seat_service.seats
  WHERE seat_map_id = :'clone_map_id'::uuid AND position_x IS NOT NULL;

-- Expected (seat_count=2000): charts=602, sessions=601, seat_maps=3,
--   seats(template)=2000, seats(clone)=2000, position_x backfilled=2000.

\echo ''
\echo '==== k6 env vars ===='
\echo 'SESSION_ID  = e0000000-0000-0000-0000-0000000000aa   (01-layout-cache, 04-viewport-compare)'
\echo 'SEAT_MAP_ID = d0000000-0000-0000-0000-0000000000cc   (03-signalr-design, Draft)'
\echo 'EVENT_ID    = b0000000-0000-0000-0000-000000000001   (02-seat-api)'
\echo 'ORG_ID      = a0000000-0000-0000-0000-000000000001'
\echo '====================='

-- =============================================================
-- 5. TEARDOWN (uncomment to remove all seeded data)
-- =============================================================
-- DELETE FROM seat_service.seats     WHERE seat_map_id IN
--   ('d0000000-0000-0000-0000-0000000000aa','d0000000-0000-0000-0000-0000000000bb','d0000000-0000-0000-0000-0000000000cc');
-- DELETE FROM seat_service.seat_maps WHERE event_id = 'b0000000-0000-0000-0000-000000000001';
-- DELETE FROM event_service.sessions WHERE event_id = 'b0000000-0000-0000-0000-000000000001';
-- DELETE FROM event_service.charts   WHERE event_id = 'b0000000-0000-0000-0000-000000000001';
-- DELETE FROM event_service.events   WHERE id       = 'b0000000-0000-0000-0000-000000000001';
-- DELETE FROM event_service.org_payment_infos WHERE organization_id = 'a0000000-0000-0000-0000-000000000001';
-- DELETE FROM user_service.user_roles WHERE user_id = '<load_test_user_id>'
--   AND organization_id = 'a0000000-0000-0000-0000-000000000001';
