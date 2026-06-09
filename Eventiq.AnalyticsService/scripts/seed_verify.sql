-- Phase 2.5 acceptance — run against the eval branch after seed_data.py.

-- 1. Row counts of the core tables
SELECT 'users' AS tbl, COUNT(*) FROM user_service."Users"
UNION ALL SELECT 'orgs', COUNT(*) FROM org_service."Organizations"
UNION ALL SELECT 'events', COUNT(*) FROM event_service.events
UNION ALL SELECT 'sessions', COUNT(*) FROM event_service.sessions
UNION ALL SELECT 'seats', COUNT(*) FROM seat_service.seats
UNION ALL SELECT 'orders_paid', COUNT(*) FROM payment_service.orders WHERE status = 'Paid'
UNION ALL SELECT 'tickets', COUNT(*) FROM event_service.tickets;

-- 2. Revenue by month (expect >= 3 months)
SELECT DATE_TRUNC('month', paid_at) AS m, SUM(total_amount)
FROM payment_service.orders WHERE status = 'Paid'
GROUP BY 1 ORDER BY 1;

-- 3. Top 5 events by tickets sold
SELECT e.name, COUNT(t.id) AS sold
FROM event_service.events e
JOIN event_service.sessions s ON s.event_id = e.id
JOIN event_service.tickets  t ON t.session_id = s.id
GROUP BY e.id, e.name ORDER BY sold DESC LIMIT 5;

-- 4. Every session of an Approved/Published event must have a seat_map (expect 0 rows)
SELECT s.id
FROM event_service.sessions s
LEFT JOIN seat_service.seat_maps sm ON sm.session_id = s.id
WHERE sm.id IS NULL
  AND EXISTS (SELECT 1 FROM event_service.events e WHERE e.id = s.event_id AND e.status IN (2, 4))
LIMIT 5;

-- 5. Seat status mix (expect Sold >= 5000)
SELECT status, COUNT(*) FROM seat_service.seats GROUP BY status;

-- 6. Orgs with revenue (cross-service join works; expect >= 5 rows)
SELECT o."Name", SUM(p.total_amount)
FROM org_service."Organizations" o
JOIN payment_service.orders p ON p.org_id = o."Id" AND p.status = 'Paid'
GROUP BY o."Id", o."Name" ORDER BY 2 DESC LIMIT 5;

-- 7. VIP seats exist and some are Sold (for the VIP-ratio question)
SELECT l.name, COUNT(*) AS total, COUNT(*) FILTER (WHERE st.status = 'Sold') AS sold
FROM seat_service.seats st JOIN event_service.legends l ON l.id = st.legend_id
WHERE l.name = 'VIP' GROUP BY l.name;
