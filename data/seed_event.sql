\set load_test_org_id 'a0000000-0000-0000-0000-000000000001'
\set load_test_event_id 'b0000000-0000-0000-0000-000000000001'
\set chart_pub_id 'c0000000-0000-0000-0000-0000000000aa'
\set chart_draft_id 'c0000000-0000-0000-0000-0000000000bb'
\set session_pub_id 'e0000000-0000-0000-0000-0000000000aa'

INSERT INTO event_service.org_payment_infos
  (id, organization_id, stripe_account_id, is_active, updated_at, created_at, is_deleted)
VALUES
  (gen_random_uuid(), :'load_test_org_id'::uuid, 'acct_loadtest_fake', true, NOW(), NOW(), false)
ON CONFLICT (organization_id) DO UPDATE
  SET is_active = true, stripe_account_id = 'acct_loadtest_fake';

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

INSERT INTO event_service.charts (id, name, event_id, created_at, updated_at, is_deleted)
VALUES
  (:'chart_pub_id'::uuid,   'Load Test Chart (published)', :'load_test_event_id'::uuid, NOW(), NOW(), false),
  (:'chart_draft_id'::uuid, 'Load Test Chart (draft)',     :'load_test_event_id'::uuid, NOW(), NOW(), false)
ON CONFLICT (id) DO NOTHING;

INSERT INTO event_service.sessions
  (id, event_id, name, start_time, end_time, chart_id, created_at, updated_at, is_deleted)
VALUES
  (:'session_pub_id'::uuid, :'load_test_event_id'::uuid, 'Load Test Session (published)',
   '2026-07-01 08:00:00+00', '2026-07-01 09:00:00+00', :'chart_pub_id'::uuid, NOW(), NOW(), false)
ON CONFLICT (id) DO NOTHING;

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

SELECT 'org_payment_infos' AS tbl, COUNT(*) AS n FROM event_service.org_payment_infos
  WHERE organization_id = :'load_test_org_id'::uuid
UNION ALL SELECT 'events',   COUNT(*) FROM event_service.events
  WHERE id = :'load_test_event_id'::uuid
UNION ALL SELECT 'charts',   COUNT(*) FROM event_service.charts
  WHERE event_id = :'load_test_event_id'::uuid AND is_deleted = false
UNION ALL SELECT 'sessions', COUNT(*) FROM event_service.sessions
  WHERE event_id = :'load_test_event_id'::uuid AND is_deleted = false;
