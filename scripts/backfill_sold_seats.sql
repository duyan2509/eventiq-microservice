-- ============================================================================
-- Backfill: mark seats Sold for already-paid orders that issued a ticket
-- ----------------------------------------------------------------------------
-- Why: SeatService and EventService both defined a consumer class named
--   PaymentCompletedConsumer with MassTransit default endpoint naming, so both
--   bound to the same "payment-completed" queue and competed for messages.
--   When EventService won a message it issued the ticket, but SeatService never
--   ran seat.Sell(), leaving the seat stuck as 'Holding'. This script repairs
--   those stranded seats. Run ONCE after deploying the queue-name fix.
--
-- Safe to run on the same Neon database: SeatService (schema seat_service) and
--   EventService (schema event_service) share one physical DB, so this
--   cross-schema UPDATE works directly. A ticket row is proof the order was
--   paid and the ticket issued, so tickets.seat_id = seats.id is the link.
--
-- Status is stored as TEXT (HasConversion<string>): -> 'Sold'.
-- position_x / position_y are STORED generated columns — never written here.
--
-- An active ticket == a paid sale, so the seat MUST be Sold regardless of its
-- current status. Holds whose TTL already expired have reverted to 'Available'
-- (a double-booking risk), so we repair anything not already 'Sold' — not just
-- the seats still showing 'Holding'.
-- ============================================================================

BEGIN;

-- 1. Dry-run: how many seats will be repaired? Review this before committing.
SELECT count(*) AS stuck_seats_to_fix
FROM   seat_service.seats AS s
JOIN   event_service.tickets AS t
       ON t.seat_id = s.id AND t.is_deleted = false
WHERE  s.is_deleted = false
  AND  s.status <> 'Sold';

-- 2. The repair.
UPDATE seat_service.seats AS s
SET    status     = 'Sold',
       held_by    = NULL,
       held_until = NULL,
       updated_at = now()
FROM   event_service.tickets AS t
WHERE  t.seat_id = s.id
  AND  t.is_deleted = false
  AND  s.is_deleted = false
  AND  s.status <> 'Sold';

-- 3. Verify: should now be 0.
SELECT count(*) AS still_stuck
FROM   seat_service.seats AS s
JOIN   event_service.tickets AS t
       ON t.seat_id = s.id AND t.is_deleted = false
WHERE  s.is_deleted = false
  AND  s.status <> 'Sold';

-- Inspect the counts above. If they look right, COMMIT; otherwise ROLLBACK.
COMMIT;
-- ROLLBACK;
