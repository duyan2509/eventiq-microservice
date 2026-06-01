# Test — Seat Design / Booking (k6 suite)

k6 scripts: [`../../k6/seat-design-test/`](../../k6/seat-design-test).
Setup data: [`../../data/seed_load_test_data.sql`](../../data/README.md) (see [DEPLOY.md](../DEPLOY.md) / infrastructure.md §8).

## Run
```powershell
cd k6/seat-design-test
.\run.ps1 -SessionId e0000000-0000-0000-0000-0000000000aa -SeatMapId d0000000-0000-0000-0000-0000000000cc
```
Each script prints a console summary **and** writes `results/<name>-summary.json`.
```bash
jq '.metrics.seats_viewport_duration.values["p(95)"]' results/viewport-compare-summary.json
jq '.metrics.seats_all_bytes.values.avg, .metrics.seats_viewport_bytes.values.avg' results/viewport-compare-summary.json
```

## Metrics & targets

### 01 — Layout meta cache (`01-layout-cache.js`, ~100 VUs)
Redis output cache on `GET /seat-maps/sessions/{id}/meta`.

| Metric | Target |
|---|---|
| `layout_req_duration` p50/p95/p99 | p95 < 200 ms |
| `http_req_duration` p95/p99 | p95 < 300, p99 < 500 ms |
| `layout_error_rate` | < 1 % |
| cache miss vs hit | warmup (miss) ≫ slower than steady (hit) |

### 02 — Seat REST CRUD (`02-seat-api.js`, ~50 VUs)
Create → read meta → read `/seats` → stats → list → delete.

| Metric | Target |
|---|---|
| `seatmap_create_duration` p95 | < 600 ms |
| `seatmap_read_duration` p95 | < 300 ms |
| `seatmap_stats_duration` p95 | < 300 ms |
| `seatmap_error_rate` | < 2 % |

### 03 — SignalR collaborative designer (`03-signalr-design.js`, ~20 VUs)
connect → JoinSeatMap → AddSeat → UpdateSeats → cursor → DeleteSeats.

| Metric | Target |
|---|---|
| `signalr_connect_duration` p95 | < 2000 ms |
| `signalr_join_duration` p95 | < 3000 ms |
| `signalr_messages_received` | > 0, no drops |
| `signalr_error_rate` | < 5 % |
| Redis `seat:presence:<id>` HLEN | grows ≈ VU count, → 0 after leave |

### 04 — Viewport vs get-all (`04-viewport-compare.js`) — refactor payoff
Same endpoint, whole map vs one viewport bbox. Seed a large map (`-v seat_count=5000`).

| Metric | Target / report |
|---|---|
| `seats_all_duration` p95 | baseline |
| `seats_viewport_duration` p95 | < 200 ms, **≪ get-all** |
| `seats_all_bytes` vs `seats_viewport_bytes` (avg) | **payload reduction %** = 1 − vp/all |
| `seats_error_rate` | < 1 % |

> Headline number: latency + payload reduction of viewport vs get-all at 5000 seats.

## Local vs Azure
| Metric | Local | Azure AKS |
|---|---|---|
| 01 layout p95 | | |
| 04 viewport p95 | | |
| 04 payload reduction % | | |
| 03 join p95 | | |

Record env context: node SKU (B2s ×2), Redis tier (C1), Postgres tier (B1ms), VU counts, date.
