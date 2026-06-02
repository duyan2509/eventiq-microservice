# Test — Redis Distributed Lock concurrency (key thesis experiment)

Proves the Redlock-protected seat hold is race-free under heavy contention.

Script: [`../../k6/redis-lock-test/redis-lock-test.js`](../../k6/redis-lock-test/redis-lock-test.js).
Needs the seed (`data/seed_load_test_data.sql`) so `SESSION_ID`'s seat map has ≥ N Available seats.

## Run
```powershell
cd k6/redis-lock-test
k6 run redis-lock-test.js                 # defaults: CONTENDED=100, VUS=500
k6 run -e CONTENDED=100 -e VUS=500 redis-lock-test.js
```
The script self-resets (releases the contended seats) in setup + teardown, so it is re-runnable.

## Setup (what the script does)
- Resolves the booking seat map for `SESSION_ID` and its first **N=100** seats.
- **500 VUs**, one hold each, seat = `seatIds[(VU-1) % N]` → every seat contended by 5 VUs.

## Assertions / metrics
Script counters (printed in the summary + `results/redis-lock-summary.json`):

| Metric / check | Expected |
|---|---|
| `lock_success` (200 OK) | exactly **100** (threshold: `count<=100`) |
| `lock_conflict` (409) | **400** (threshold: `count>=400`) |
| `lock_other` (unexpected) | **0** (threshold: `count==0`) |
| `lock_hold_duration` p95 | record |
| DB rows `Holding` for those seats | **100, no duplicates** (verify by SQL below) |
| Redis keys `seat-lock:*` | short-lived, released after each hold |

DB verification after a run:
```sql
SELECT count(*) FROM seat_service.seats
WHERE seat_map_id='d0000000-0000-0000-0000-0000000000bb' AND status='Holding';  -- must equal 100
```

## Lock vs no-lock comparison (the contribution)
Run twice and tabulate:

| Run | 200 OK | 409 | Duplicate Holding | Verdict |
|---|---|---|---|---|
| **Redlock ON** | 100 | 400 | 0 | correct |
| **Redlock OFF** (disabled) | >100 | <400 | >0 | over-sell / race |

Disabling Redlock should visibly break correctness (over-selling), demonstrating
why the distributed lock is required.

## Local vs Azure
| Metric | Local | Azure AKS |
|---|---|---|
| 200 / 409 / duplicate | | correctness must hold on both |
| hold p95 latency | | |
