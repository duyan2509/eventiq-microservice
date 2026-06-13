\set load_test_org_id 'a0000000-0000-0000-0000-000000000001'
\set load_test_event_id 'b0000000-0000-0000-0000-000000000001'
\set chart_pub_id 'c0000000-0000-0000-0000-0000000000aa'
\set chart_draft_id 'c0000000-0000-0000-0000-0000000000bb'
\set session_pub_id 'e0000000-0000-0000-0000-0000000000aa'
\set template_map_id 'd0000000-0000-0000-0000-0000000000aa'
\set clone_map_id 'd0000000-0000-0000-0000-0000000000bb'
\set draft_map_id 'd0000000-0000-0000-0000-0000000000cc'

\if :{?seat_count}
\else
  \set seat_count 2000
\endif

INSERT INTO seat_service.seat_maps
  (id, chart_id, event_id, organization_id, session_id, name, status,
   canvas_settings, version, total_seats, created_at, updated_at, is_deleted)
VALUES
  (:'template_map_id'::uuid, :'chart_pub_id'::uuid, :'load_test_event_id'::uuid,
   :'load_test_org_id'::uuid, NULL, 'LT Template', 'Published',
   NULL, 1, :seat_count, NOW(), NULL, false)
ON CONFLICT (id) DO NOTHING;

INSERT INTO seat_service.seat_maps
  (id, chart_id, event_id, organization_id, session_id, name, status,
   canvas_settings, version, total_seats, created_at, updated_at, is_deleted)
VALUES
  (:'clone_map_id'::uuid, :'chart_pub_id'::uuid, :'load_test_event_id'::uuid,
   :'load_test_org_id'::uuid, :'session_pub_id'::uuid, 'LT Template', 'Published',
   NULL, 1, :seat_count, NOW(), NULL, false)
ON CONFLICT (id) DO NOTHING;

INSERT INTO seat_service.seat_maps
  (id, chart_id, event_id, organization_id, session_id, name, status,
   canvas_settings, version, total_seats, created_at, updated_at, is_deleted)
VALUES
  (:'draft_map_id'::uuid, :'chart_draft_id'::uuid, :'load_test_event_id'::uuid,
   :'load_test_org_id'::uuid, NULL, 'LT Draft (SignalR)', 'Draft',
   NULL, 1, 0, NOW(), NULL, false)
ON CONFLICT (id) DO NOTHING;

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

SELECT 'seat_maps' AS tbl, COUNT(*) AS n FROM seat_service.seat_maps
  WHERE event_id = :'load_test_event_id'::uuid AND is_deleted = false
UNION ALL SELECT 'seats (template)', COUNT(*) FROM seat_service.seats
  WHERE seat_map_id = :'template_map_id'::uuid AND is_deleted = false
UNION ALL SELECT 'seats (clone)', COUNT(*) FROM seat_service.seats
  WHERE seat_map_id = :'clone_map_id'::uuid AND is_deleted = false
UNION ALL SELECT 'position_x backfilled', COUNT(*) FROM seat_service.seats
  WHERE seat_map_id = :'clone_map_id'::uuid AND position_x IS NOT NULL;
