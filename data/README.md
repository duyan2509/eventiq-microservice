# data/ — load-test & demo seed

`seed_load_test_data.sql` seeds a complete, deterministic dataset across all
service schemas so the k6 suite (and a quick demo) can run without driving the
async approval/clone pipeline. All IDs are **fixed** so scripts can hardcode them.

## How to run
```bash
# STEP 0 (once): start services so UserService seeds roles, then register the user
#   curl -X POST <gateway>/auth/register -H "Content-Type: application/json" \
#     -d '{"email":"loadtest@eventiq.dev","password":"LoadTest@123","username":"loadtest"}'
#   SELECT id FROM user_service.users WHERE email='loadtest@eventiq.dev';

psql "<connection-string>" \
  -v load_test_user_id="'<uuid-of-loadtest-user>'" \
  -f data/seed_load_test_data.sql
# optional larger map for the viewport test:  -v seat_count=5000   (default 2000)
```

## What gets seeded

| Schema | Data |
|---|---|
| `user_service` | links the load-test user to the **Organization** role for the load-test org |
| `event_service` | org payment info (fake Stripe), 1 **event** (`Approved`), 602 charts, 601 sessions |
| `seat_service` | a Published **template** seat map + a Published **session clone** (each with `seat_count` flat seats on a grid) + an empty **Draft** seat map |

Seats are flat (`position` JSONB `{x,y}`); the `position_x`/`position_y` generated
columns are populated automatically by Postgres.

## Fixed IDs

| ID | Value | Purpose |
|---|---|---|
| ORG_ID | `a0000000-0000-0000-0000-000000000001` | load-test org |
| EVENT_ID | `b0000000-0000-0000-0000-000000000001` | load-test event (Approved) |
| chart (published) | `c0000000-0000-0000-0000-0000000000aa` | chart for the published map |
| chart (draft) | `c0000000-0000-0000-0000-0000000000bb` | chart for the draft map |
| **SESSION_ID** | `e0000000-0000-0000-0000-0000000000aa` | session with the published seat map |
| template map | `d0000000-0000-0000-0000-0000000000aa` | Published template (session_id NULL) |
| clone map | `d0000000-0000-0000-0000-0000000000bb` | Published clone for SESSION_ID |
| **SEAT_MAP_ID** | `d0000000-0000-0000-0000-0000000000cc` | empty **Draft** map (org-owned) |

## Which test uses what

| Test | Needs | Why |
|---|---|---|
| `01-layout-cache` | `SESSION_ID` | reads `/seat-maps/sessions/{id}/meta` (cached) |
| `02-seat-api` | `EVENT_ID`, `ORG_ID` | create/list/stats/delete seat maps |
| `03-signalr-design` | `SEAT_MAP_ID` (Draft) | AddSeat/UpdateSeats require a **Draft** org-owned map |
| `04-viewport-compare` | `SESSION_ID` | compares `/seats` (all) vs `/seats?bbox` (viewport) |

> Defaults for `SESSION_ID` / `SEAT_MAP_ID` are baked into `k6/seat-design-test/config.js`,
> so the scripts run turnkey after seeding.

## Teardown
The bottom of the SQL file has a commented teardown block — uncomment to delete
all seeded rows (seats → seat_maps → sessions → charts → event → org payment → user role).
