-- =============================================================
-- Demo Seed — Defense Presentation
-- =============================================================
-- Creates:
--   • Organization entity + 1 permission + 2 members
--   • 20 events with banners (past/current/upcoming)
--   • Legends, sessions, seat maps + seats (upcoming)
--   • 600 Paid orders + tickets with check-in data (past)
--
-- Prerequisites:
--   1. Start services once (UserService seeds roles).
--   2. Register the demo org user via API.
--   3. Get its UUID: SELECT "Id" FROM user_service."Users" WHERE "Email"='...';
--
-- Run:
--   psql "<neon-conn>" \
--     -v demo_user_id="'<org-owner-uuid>'" \
--     -f data/seed_demo.sql
--
-- Idempotent: ON CONFLICT DO NOTHING on all inserts.
-- =============================================================

-- Fixed IDs
\set demo_org_id      'a0000000-0000-0000-0000-000000000001'
\set demo_perm_id     'a0000000-0000-0000-0000-000000000099'  -- Staff permission

-- =============================================================
-- 1. ORGANIZATION ENTITY
-- =============================================================
INSERT INTO org_service."Organizations"
  ("Id", "Name", "Description", "OwnerId", "OwnerEmail",
   "StripeAccountId", "PaymentStatus", "PaymentConfiguredAt",
   "CreatedAt", "UpdatedAt", "IsDeleted")
VALUES (
  :'demo_org_id'::uuid,
  'Eventiq Demo',
  'Tổ chức tổ chức sự kiện âm nhạc và giải trí hàng đầu Việt Nam.',
  :'demo_user_id'::uuid,
  'demo@eventiq.dev',
  'acct_demo_stripe',
  2,                        -- Active
  NOW() - interval '90 days',
  NOW() - interval '90 days', NOW(), false
) ON CONFLICT DO NOTHING;

-- =============================================================
-- 2. PERMISSION + MEMBERS
-- =============================================================
INSERT INTO org_service."Permissions"
  ("Id", "Name", "OrganizationId", "IsDesigner", "CreatedAt", "IsDeleted")
VALUES (
  :'demo_perm_id'::uuid, 'Staff', :'demo_org_id'::uuid, true, NOW(), false
) ON CONFLICT DO NOTHING;

INSERT INTO org_service."Members"
  ("Id", "UserId", "Email", "OrganizationId", "PermissionId", "CreatedAt", "IsDeleted")
VALUES
  (gen_random_uuid(), :'demo_user_id'::uuid, 'demo@eventiq.dev',
   :'demo_org_id'::uuid, :'demo_perm_id'::uuid, NOW(), false),
  (gen_random_uuid(), NULL, 'staff1@eventiq.dev',
   :'demo_org_id'::uuid, :'demo_perm_id'::uuid, NOW(), false),
  (gen_random_uuid(), NULL, 'staff2@eventiq.dev',
   :'demo_org_id'::uuid, :'demo_perm_id'::uuid, NOW(), false)
ON CONFLICT DO NOTHING;

-- =============================================================
-- 3. ORG PAYMENT INFO (idempotent)
-- =============================================================
INSERT INTO event_service.org_payment_infos
  (id, organization_id, stripe_account_id, is_active, updated_at, created_at, is_deleted)
VALUES
  (gen_random_uuid(), :'demo_org_id'::uuid, 'acct_demo_stripe', true, NOW(), NOW(), false)
ON CONFLICT (organization_id) DO UPDATE SET is_active = true;

-- =============================================================
-- 4. EVENTS + SESSIONS + LEGENDS + SEAT MAPS + ORDERS
-- =============================================================
DO $$
DECLARE
  v_user_id  uuid := :'demo_user_id'::uuid;
  v_org_id   uuid := 'a0000000-0000-0000-0000-000000000001';
  v_org_name text := 'Eventiq Demo';

  v_row        text[];
  v_event_id   uuid;
  v_chart_id   uuid;
  v_session_id uuid;
  v_map_id     uuid;
  v_leg_std    uuid;
  v_leg_vip    uuid;
  v_order_id   uuid;
  v_ticket_id  uuid;
  v_seat_id    uuid;
  v_price_std  int;
  v_price_vip  int;
  v_n          int;
  v_idx        int := 0;
  v_total      numeric(18,2);
  v_fee        numeric(18,2);
  v_paid_at    timestamptz;
  v_checked_in bool;
  v_seats      int := 200;
  v_cols       int;

  -- Picsum seeds give stable, pretty landscape photos (concerts/events theme)
  banners text[] := ARRAY[
    'https://picsum.photos/seed/eq-music1/800/400',
    'https://picsum.photos/seed/eq-jazz/800/400',
    'https://picsum.photos/seed/eq-art/800/400',
    'https://picsum.photos/seed/eq-rock/800/400',
    'https://picsum.photos/seed/eq-food/800/400',
    'https://picsum.photos/seed/eq-gala/800/400',
    'https://picsum.photos/seed/eq-tet/800/400',
    'https://picsum.photos/seed/eq-spring/800/400',
    'https://picsum.photos/seed/eq-indie/800/400',
    'https://picsum.photos/seed/eq-love/800/400',
    'https://picsum.photos/seed/eq-summer/800/400',
    'https://picsum.photos/seed/eq-awards/800/400',
    'https://picsum.photos/seed/eq-edm/800/400',
    'https://picsum.photos/seed/eq-c1/800/400',
    'https://picsum.photos/seed/eq-c2/800/400',
    'https://picsum.photos/seed/eq-c3/800/400',
    'https://picsum.photos/seed/eq-c4/800/400',
    'https://picsum.photos/seed/eq-c5/800/400',
    'https://picsum.photos/seed/eq-c6/800/400',
    'https://picsum.photos/seed/eq-nye/800/400'
  ];

  -- Columns: [1]name [2]province_code [3]city [4]start_time [5]end_time
  --          [6]kind(past/current/upcoming) [7]std_price [8]vip_price
  events_data text[][] := ARRAY[
    ARRAY['Lễ Hội Âm Nhạc Mùa Hè 2025',     'VN-70', 'Bình Dương',       '2025-07-12 18:00+07', '2025-07-12 22:00+07', 'past',     '350000', '750000'],
    ARRAY['Jazz Night Sài Gòn',              'VN-SG', 'TP. Hồ Chí Minh', '2025-08-23 19:30+07', '2025-08-23 23:00+07', 'past',     '400000', '900000'],
    ARRAY['Triển Lãm Nghệ Thuật Đương Đại', 'VN-HN', 'Hà Nội',           '2025-09-05 09:00+07', '2025-09-07 21:00+07', 'past',     '200000', '500000'],
    ARRAY['Rock Fest Hà Nội 2025',           'VN-HN', 'Hà Nội',           '2025-10-18 17:00+07', '2025-10-18 23:30+07', 'past',     '450000', '950000'],
    ARRAY['Hội Chợ Ẩm Thực Quốc Tế',       'VN-DN', 'Đà Nẵng',          '2025-11-08 10:00+07', '2025-11-10 22:00+07', 'past',     '150000', '350000'],
    ARRAY['Year-End Gala 2025',              'VN-SG', 'TP. Hồ Chí Minh', '2025-12-27 19:00+07', '2025-12-27 23:59+07', 'past',     '600000', '1500000'],
    ARRAY['Tết Concert 2026',                'VN-SG', 'TP. Hồ Chí Minh', '2026-01-24 18:00+07', '2026-01-24 22:00+07', 'past',     '500000', '1200000'],
    ARRAY['Spring Acoustic Night',           'VN-HN', 'Hà Nội',           '2026-03-14 19:30+07', '2026-03-14 22:30+07', 'past',     '300000', '700000'],
    ARRAY['Đêm Nhạc Indie Hà Nội',          'VN-HN', 'Hà Nội',           '2026-04-19 19:00+07', '2026-04-19 23:00+07', 'past',     '280000', '650000'],
    ARRAY['Love Songs Concert',              'VN-SG', 'TP. Hồ Chí Minh', '2026-05-10 18:30+07', '2026-05-10 22:00+07', 'past',     '350000', '800000'],
    ARRAY['Summer Vibes 2026',               'VN-SG', 'TP. Hồ Chí Minh', '2026-06-01 17:00+07', '2026-06-30 23:00+07', 'current',  '420000', '980000'],
    ARRAY['Vietnam Music Awards 2026',       'VN-HN', 'Hà Nội',           '2026-06-21 19:00+07', '2026-06-21 23:00+07', 'current',  '550000', '1300000'],
    ARRAY['Đêm Nhạc EDM Đà Nẵng',          'VN-DN', 'Đà Nẵng',          '2026-06-28 21:00+07', '2026-06-29 03:00+07', 'current',  '380000', '880000'],
    ARRAY['Concert Mùa Hè 2026',            'VN-SG', 'TP. Hồ Chí Minh', '2026-07-19 18:00+07', '2026-07-19 22:00+07', 'upcoming', '400000', '950000'],
    ARRAY['Đêm Nhạc Truyền Thống',         'VN-HN', 'Hà Nội',           '2026-08-15 18:30+07', '2026-08-15 22:00+07', 'upcoming', '250000', '600000'],
    ARRAY['Art & Culture Festival 2026',     'VN-DN', 'Đà Nẵng',          '2026-09-05 09:00+07', '2026-09-07 22:00+07', 'upcoming', '180000', '420000'],
    ARRAY['Halloween Night Show',            'VN-SG', 'TP. Hồ Chí Minh', '2026-10-31 20:00+07', '2026-11-01 02:00+07', 'upcoming', '480000', '1100000'],
    ARRAY['Acoustic Evening November',      'VN-HN', 'Hà Nội',           '2026-11-14 19:30+07', '2026-11-14 22:30+07', 'upcoming', '300000', '700000'],
    ARRAY['Christmas Gala Concert',          'VN-SG', 'TP. Hồ Chí Minh', '2026-12-24 19:00+07', '2026-12-24 23:00+07', 'upcoming', '550000', '1400000'],
    ARRAY['New Year Countdown 2027',         'VN-SG', 'TP. Hồ Chí Minh', '2026-12-31 20:00+07', '2027-01-01 01:00+07', 'upcoming', '700000', '1800000']
  ];

BEGIN
  v_cols := ceil(sqrt(v_seats::numeric))::int;

  FOREACH v_row SLICE 1 IN ARRAY events_data LOOP
    v_idx        := v_idx + 1;
    v_event_id   := gen_random_uuid();
    v_chart_id   := gen_random_uuid();
    v_session_id := gen_random_uuid();
    v_leg_std    := gen_random_uuid();
    v_leg_vip    := gen_random_uuid();
    v_price_std  := v_row[7]::int;
    v_price_vip  := v_row[8]::int;

    -- Event (with banner)
    INSERT INTO event_service.events
      (id, organization_id, organization_name, name, description,
       detail_address, province_code, commune_code, province_name, commune_name,
       event_banner, status, start_time, end_time, created_at, updated_at, is_deleted)
    VALUES (
      v_event_id, v_org_id, v_org_name, v_row[1],
      'Sự kiện đặc sắc do ' || v_org_name || ' tổ chức tại ' || v_row[3] || '. Hãy cùng trải nghiệm những khoảnh khắc âm nhạc đỉnh cao!',
      '1 Đường Lê Lợi', v_row[2], '000', v_row[3], 'Quận 1',
      banners[v_idx],
      4,
      v_row[4]::timestamptz, v_row[5]::timestamptz,
      NOW(), NOW(), false
    ) ON CONFLICT DO NOTHING;

    -- Chart
    INSERT INTO event_service.charts (id, name, event_id, created_at, updated_at, is_deleted)
    VALUES (v_chart_id, v_row[1], v_event_id, NOW(), NOW(), false)
    ON CONFLICT DO NOTHING;

    -- Session
    INSERT INTO event_service.sessions
      (id, event_id, name, start_time, end_time, chart_id, created_at, updated_at, is_deleted)
    VALUES (
      v_session_id, v_event_id, 'Suất chính — ' || v_row[1],
      v_row[4]::timestamptz, v_row[5]::timestamptz,
      v_chart_id, NOW(), NOW(), false
    ) ON CONFLICT DO NOTHING;

    -- Legends (Standard 70% seats, VIP 30%)
    INSERT INTO event_service.legends (id, name, color, price, event_id, created_at, updated_at, is_deleted)
    VALUES
      (v_leg_std, 'Standard', '#6366f1', v_price_std, v_event_id, NOW(), NOW(), false),
      (v_leg_vip, 'VIP',      '#f59e0b', v_price_vip, v_event_id, NOW(), NOW(), false)
    ON CONFLICT DO NOTHING;

    -- Seat map + seats (upcoming only — bookable)
    IF v_row[6] = 'upcoming' THEN
      v_map_id := gen_random_uuid();

      INSERT INTO seat_service.seat_maps
        (id, chart_id, event_id, organization_id, session_id, name, status,
         canvas_settings, version, total_seats, created_at, updated_at, is_deleted)
      VALUES (
        v_map_id, v_chart_id, v_event_id, v_org_id, v_session_id,
        v_row[1], 'Published', NULL, 1, v_seats, NOW(), NULL, false
      ) ON CONFLICT DO NOTHING;

      INSERT INTO seat_service.seats
        (id, seat_map_id, label, seat_number, status, seat_type, position,
         legend_id, custom_properties, held_by, held_until, created_at, updated_at, is_deleted)
      SELECT
        gen_random_uuid(), v_map_id,
        'S' || i, i, 'Available', 1,
        jsonb_build_object('x', ((i-1) % v_cols) * 30, 'y', ((i-1) / v_cols) * 30),
        CASE WHEN i <= (v_seats * 0.7)::int THEN v_leg_std ELSE v_leg_vip END,
        NULL, NULL, NULL, NOW(), NULL, false
      FROM generate_series(1, v_seats) i
      ON CONFLICT DO NOTHING;
    END IF;

    -- Orders + tickets (past events: 60 orders, ~70% checked in)
    IF v_row[6] = 'past' THEN
      FOR v_n IN 1..60 LOOP
        v_order_id   := gen_random_uuid();
        v_ticket_id  := gen_random_uuid();
        v_seat_id    := gen_random_uuid();
        v_total      := CASE WHEN v_n % 3 = 0 THEN v_price_vip ELSE v_price_std END;
        v_fee        := round(v_total * 0.05, 2);
        v_checked_in := (v_n % 10 < 7);  -- 70% checked in
        -- Spread paid_at evenly across the month before the event
        v_paid_at := v_row[4]::timestamptz - interval '30 days'
                     + (((v_n - 1)::numeric / 59) * interval '28 days');

        INSERT INTO payment_service.orders
          (id, user_id, org_id, session_id, stripe_session_id, status,
           total_amount, platform_fee, event_name, session_name, session_date,
           paid_at, created_at, updated_at, is_deleted)
        VALUES (
          v_order_id, v_user_id, v_org_id, v_session_id,
          'cs_demo_' || left(v_order_id::text, 8),
          'Paid', v_total, v_fee,
          v_row[1], 'Suất chính — ' || v_row[1], v_row[4]::timestamptz,
          v_paid_at, v_paid_at, v_paid_at, false
        ) ON CONFLICT DO NOTHING;

        INSERT INTO payment_service.order_items
          (id, order_id, seat_id, seat_label, legend_name, price,
           created_at, updated_at, is_deleted)
        VALUES (
          gen_random_uuid(), v_order_id, v_seat_id,
          'S' || v_n,
          CASE WHEN v_n % 3 = 0 THEN 'VIP' ELSE 'Standard' END,
          v_total, v_paid_at, v_paid_at, false
        ) ON CONFLICT DO NOTHING;

        INSERT INTO event_service.tickets
          (id, order_id, session_id, seat_id, seat_label, legend_name,
           price, qr_code, is_checked_in, checked_in_at, issued_at,
           created_at, updated_at, is_deleted)
        VALUES (
          v_ticket_id, v_order_id, v_session_id, v_seat_id,
          'S' || v_n,
          CASE WHEN v_n % 3 = 0 THEN 'VIP' ELSE 'Standard' END,
          v_total,
          'QR-' || left(v_order_id::text, 8) || '-' || v_n,
          v_checked_in,
          CASE WHEN v_checked_in THEN v_row[4]::timestamptz + interval '30 min' * v_n ELSE NULL END,
          v_paid_at, v_paid_at, v_paid_at, false
        ) ON CONFLICT DO NOTHING;

      END LOOP;
    END IF;

  END LOOP;
END $$;

-- =============================================================
-- VERIFY
-- =============================================================
SELECT 'org entity'          AS item, COUNT(*)::text AS count FROM org_service."Organizations"  WHERE "Id" = 'a0000000-0000-0000-0000-000000000001'
UNION ALL SELECT 'members',           COUNT(*)::text FROM org_service."Members"       WHERE "OrganizationId" = 'a0000000-0000-0000-0000-000000000001' AND "IsDeleted" = false
UNION ALL SELECT 'demo events',       COUNT(*)::text FROM event_service.events        WHERE organization_id  = 'a0000000-0000-0000-0000-000000000001' AND name NOT LIKE '[LOAD TEST]%' AND is_deleted = false
UNION ALL SELECT 'legends',           COUNT(*)::text FROM event_service.legends       WHERE is_deleted = false
UNION ALL SELECT 'seat_maps (Pub)',   COUNT(*)::text FROM seat_service.seat_maps      WHERE organization_id  = 'a0000000-0000-0000-0000-000000000001' AND status = 'Published' AND is_deleted = false
UNION ALL SELECT 'seats',             COUNT(*)::text FROM seat_service.seats          WHERE is_deleted = false
UNION ALL SELECT 'orders (Paid)',     COUNT(*)::text FROM payment_service.orders      WHERE stripe_session_id LIKE 'cs_demo_%' AND status = 'Paid'
UNION ALL SELECT 'tickets',           COUNT(*)::text FROM event_service.tickets       WHERE qr_code LIKE 'QR-%' AND is_deleted = false
UNION ALL SELECT 'tickets checked-in',COUNT(*)::text FROM event_service.tickets       WHERE qr_code LIKE 'QR-%' AND is_checked_in = true;

\echo ''
\echo 'Demo seed complete.'
\echo '  10 past  × 60 orders (70% checked-in) = 600 Paid orders'
\echo '   3 current events (Published, no seat maps)'
\echo '   7 upcoming × 200 seats (Standard + VIP, bookable)'

-- =============================================================
-- TEARDOWN (uncomment to wipe)
-- =============================================================
-- DELETE FROM event_service.tickets        WHERE qr_code LIKE 'QR-%';
-- DELETE FROM payment_service.order_items  WHERE order_id IN (SELECT id FROM payment_service.orders WHERE stripe_session_id LIKE 'cs_demo_%');
-- DELETE FROM payment_service.orders       WHERE stripe_session_id LIKE 'cs_demo_%';
-- DELETE FROM seat_service.seats           WHERE seat_map_id IN (SELECT id FROM seat_service.seat_maps WHERE organization_id = 'a0000000-0000-0000-0000-000000000001' AND name NOT LIKE 'LT %');
-- DELETE FROM seat_service.seat_maps       WHERE organization_id = 'a0000000-0000-0000-0000-000000000001' AND name NOT LIKE 'LT %';
-- DELETE FROM event_service.legends        WHERE event_id IN (SELECT id FROM event_service.events WHERE organization_id = 'a0000000-0000-0000-0000-000000000001' AND name NOT LIKE '[LOAD TEST]%');
-- DELETE FROM event_service.sessions       WHERE event_id IN (SELECT id FROM event_service.events WHERE organization_id = 'a0000000-0000-0000-0000-000000000001' AND name NOT LIKE '[LOAD TEST]%');
-- DELETE FROM event_service.charts         WHERE event_id IN (SELECT id FROM event_service.events WHERE organization_id = 'a0000000-0000-0000-0000-000000000001' AND name NOT LIKE '[LOAD TEST]%');
-- DELETE FROM event_service.events         WHERE organization_id = 'a0000000-0000-0000-0000-000000000001' AND name NOT LIKE '[LOAD TEST]%';
-- DELETE FROM org_service."Members"        WHERE "OrganizationId" = 'a0000000-0000-0000-0000-000000000001';
-- DELETE FROM org_service."Permissions"    WHERE "OrganizationId" = 'a0000000-0000-0000-0000-000000000001';
-- DELETE FROM org_service."Organizations"  WHERE "Id" = 'a0000000-0000-0000-0000-000000000001';
