-- Cross-service smoke test. Works identically in dev (Neon native)
-- and prod (FDW) because schema prefixes are the same.

-- 1. Paid orders join user
SELECT u."Username",
       o.event_name,
       o.total_amount,
       o.paid_at
FROM payment_service.orders o
JOIN user_service."Users" u ON u."Id" = o.user_id
WHERE o.status = 'Paid'
LIMIT 5;

-- 2. Event ↔ organization join (snake_case ↔ PascalCase quoting)
SELECT e.name AS event_name,
       org."Name" AS organization_name,
       e.status,
       e.start_time
FROM event_service.events e
JOIN org_service."Organizations" org ON org."Id" = e.organization_id
LIMIT 5;

-- 3. Seat ↔ legend (event-service crosses into seat-service)
SELECT s.label,
       s.status,
       l.name AS legend_name,
       l.price
FROM seat_service.seats s
LEFT JOIN event_service.legends l ON l.id = s.legend_id
LIMIT 5;

-- 4. Count tables per service (acceptance check)
SELECT table_schema, COUNT(*) AS n
FROM information_schema.tables
WHERE table_schema IN
      ('user_service','org_service','event_service','seat_service','payment_service')
GROUP BY 1
ORDER BY 1;
