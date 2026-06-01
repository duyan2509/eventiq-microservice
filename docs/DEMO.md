# DEMO — EventIQ defense runbook

End-to-end script for the thesis defense. It walks the full lifecycle and
highlights the **three thesis contributions**:

1. **Redis Distributed Lock** — race-free concurrent seat holds
2. **Collaborative Seat Designer** + **viewport seat streaming**
3. **Text2SQL analytics** — natural language → SQL → chart

## Accounts

| Role | Account | Use |
|---|---|---|
| Admin | `eventiq@gmail.com` / `Admin@123` | approve events, platform analytics |
| Org owner | `owner@demo.com` | create/design/publish event |
| Customer | `customer@demo.com` | browse + book + pay |
| Staff | `staff@demo.com` | check-in at the door |

## Pre-demo prep (do before the room)
1. `.\run-all.ps1` + `npm run dev` (see [DEPLOY.md](./DEPLOY.md)); confirm all services green.
2. Seed a clean demo org/event with Stripe Connect configured and a few price tiers.
3. Have two browser windows ready (org owner + a second designer) for the collab demo.
4. Pre-open: admin panel, seat designer, booking page, analytics page.

---

## Flow (≈10–12 min)

### A. Organizer creates & designs (contribution #2)
1. Login as **owner@demo.com** → create Organization (Stripe Connect already linked).
2. Create an **Event** (banner upload) → add a **Session** + **Chart** + **price-tier legends**.
3. Open the **Seat Designer** for the chart:
   - Draw a row (drag), **Auto-label**, assign **seat types** + **legends**.
   - **Open a second window** as another designer → show **live cursors + real-time sync**
     (SignalR): a seat added in one window appears instantly in the other.
   - **Publish** the seat map.
4. **Submit** the event for review.

### B. Admin approves (triggers clone)
5. Login as **admin** → **approve** the submission.
   → `EventApproved` fan-out clones the published template into a per-session seat map.

### C. Customer books — Redis Lock (contribution #1)
6. Login as **customer@demo.com** → open the event's **booking page**.
   - Note **viewport streaming**: seats load for the visible area, more load as you pan
     (large venues stay fast — see [test-seat-design](./test-seat-design/README.md)).
   - Select seats → **Go to Checkout** (server **holds** them via Redlock, 10-min TTL).
   - *(Optional punch:* a second customer trying the same seat gets **409 Conflict** —
     this is the Redlock guarantee; the load test proves it at 500 VUs.)*

### D. Payment (contribution: Stripe)
7. Complete **Stripe Checkout** (test card `4242 4242 4242 4242`).
   → webhook marks seats **Sold**, issues an HMAC-signed ticket + QR.

### E. Check-in
8. Login as **staff@demo.com** → `/checkin` → enter the booking code → **checked-in**;
   re-scanning the same code is rejected (duplicate guard).

### F. Analytics — Text2SQL (contribution #3)
9. Login as **admin / org** → **Analytics** → ask in natural language, e.g.
   *"revenue per event last month"* or *"top 5 organizations by ticket sales"*.
   → pipeline generates SQL (graph-based schema linking, Groq LLM) → renders a chart.
   Show 2–3 questions of increasing difficulty.

---

## Closing talking points
- **Redlock**: at 500 concurrent VUs on 100 seats → exactly 100 `200 OK`, 400 `409`, **zero** duplicate holds — see [test-redis-lock](./test-redis-lock/README.md).
- **Seat designer**: real-time multi-user; viewport streaming cuts payload/latency on large venues — see [test-seat-design](./test-seat-design/README.md).
- **Text2SQL**: 7-stage pipeline, graph schema linking, runs on a free LLM tier — see [test-text2sql](./test-text2sql/README.md).
