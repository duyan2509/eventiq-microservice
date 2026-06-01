# Test — Redis Distributed Lock concurrency (key thesis experiment)

Proves the Redlock-protected seat hold is race-free under heavy contention.

> Script `redis-lock-test.js` is **not yet written** — this folder documents the
> target design + metrics to collect. (Tracked in `remaining_work.md` Test 2.)

## Setup
- 1 event, **100 seats** seeded `Available`.
- **500 VUs** concurrently `POST /seat-maps/{id}/seats/hold` over the **same 100
  overlapping seats** (each VU requests a random/overlapping subset).

## Assertions / metrics
| Assertion | Expected |
|---|---|
| total `200 OK` (successful holds) | exactly **100** |
| total `409 Conflict` | exactly **400** |
| DB rows in `Holding` for those seats | **100, no duplicates** |
| hold p95 latency | record |
| Redlock keys `seat-lock:*` | short-lived, released after hold |

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
