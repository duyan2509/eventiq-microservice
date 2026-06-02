/**
 * redis-lock-test.js — Redis Distributed Lock (Redlock) concurrency experiment
 *
 * The core thesis experiment. N seats are contended by many VUs: each VU tries to
 * HOLD exactly one seat, mapped seat = seatIds[(VU-1) % N]. With CONTENDED=100 and
 * VUS=500, every seat is requested by 5 VUs simultaneously.
 *
 *   Redlock ON  → exactly N `200 OK`, the rest `409 Conflict`, no duplicate Holding.
 *   Redlock OFF → over-sell: more than N successes / duplicate Holding (race).
 *
 * Hold endpoint: POST /seat-maps/{seatMapId}/seats/hold  body {seatIds:[...]}
 *   200 → {seatIds,status,heldUntil} ; 409 → {error}  (all-or-nothing per request)
 *
 * Prereqs: seed data applied (data/seed_load_test_data.sql) so SESSION_ID has a
 * published seat map with ≥ CONTENDED Available seats.
 *
 * Run:
 *   k6 run redis-lock-test.js
 *   k6 run -e CONTENDED=100 -e VUS=500 redis-lock-test.js
 *
 * Verify "no duplicate Holding" in the DB afterwards:
 *   SELECT count(*) FROM seat_service.seats
 *   WHERE seat_map_id='<clone map>' AND status='Holding';   -- must equal N
 */

import http from 'k6/http'
import { check } from 'k6'
import { Counter, Trend } from 'k6/metrics'
import { BASE_URL, SESSION_ID, loginOrg, authHeaders } from '../seat-design-test/config.js'

const N    = Number(__ENV.CONTENDED || 100)   // contended seats
const VUS  = Number(__ENV.VUS || 500)         // concurrent VUs

const lockSuccess  = new Counter('lock_success')   // 200 OK
const lockConflict = new Counter('lock_conflict')  // 409 Conflict
const lockOther    = new Counter('lock_other')     // anything unexpected
const holdDuration = new Trend('lock_hold_duration', true)

export const options = {
  scenarios: {
    contend: {
      executor: 'per-vu-iterations',
      vus: VUS,
      iterations: 1,           // each VU makes exactly one hold attempt
      maxDuration: '60s',
    },
  },
  thresholds: {
    // Locking is correct iff at most N holds succeed and nothing errors.
    lock_success:  [`count<=${N}`],
    lock_other:    ['count==0'],
    lock_conflict: [`count>=${VUS - N}`],
  },
}

export function setup() {
  const token = loginOrg()
  const h = authHeaders(token)

  // Resolve the booking seat map for this session + its first N seat IDs.
  const meta = http.get(`${BASE_URL}/seat-maps/sessions/${SESSION_ID}/meta`, { headers: h })
  if (meta.status !== 200) throw new Error(`meta failed: ${meta.status} — seed applied?`)
  const seatMapId = JSON.parse(meta.body).id

  const seatsRes = http.get(`${BASE_URL}/seat-maps/sessions/${SESSION_ID}/seats`, { headers: h })
  if (seatsRes.status !== 200) throw new Error(`seats failed: ${seatsRes.status}`)
  const seatIds = JSON.parse(seatsRes.body).seats.slice(0, N).map(s => s.id)
  if (seatIds.length < N) throw new Error(`need ${N} seats, found ${seatIds.length}`)

  // Reset: release any leftover holds from a previous run (same user), best-effort.
  http.del(`${BASE_URL}/seat-maps/${seatMapId}/seats/hold`,
    JSON.stringify({ seatIds }), { headers: h })

  return { token, seatMapId, seatIds }
}

export default function ({ token, seatMapId, seatIds }) {
  const seatId = seatIds[(__VU - 1) % N]   // 5 VUs per seat at VUS=500, N=100
  const res = http.post(
    `${BASE_URL}/seat-maps/${seatMapId}/seats/hold`,
    JSON.stringify({ seatIds: [seatId] }),
    { headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, tags: { name: 'hold' } }
  )
  holdDuration.add(res.timings.duration)

  if (res.status === 200)      lockSuccess.add(1)
  else if (res.status === 409) lockConflict.add(1)
  else                         lockOther.add(1)

  check(res, { '200 or 409': r => r.status === 200 || r.status === 409 })
}

export function teardown({ token, seatMapId, seatIds }) {
  // Release all contended seats so the dataset is reusable for the next run.
  http.del(`${BASE_URL}/seat-maps/${seatMapId}/seats/hold`,
    JSON.stringify({ seatIds }),
    { headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' } })
}

export function handleSummary(data) {
  const d = data.metrics
  const c = k => d[k]?.values?.count ?? 0
  const success = c('lock_success'), conflict = c('lock_conflict'), other = c('lock_other')

  console.log('\n===== Redis Lock Concurrency Summary =====')
  console.log(`Contended seats (N) : ${N}`)
  console.log(`Concurrent VUs      : ${VUS}`)
  console.log(`200 OK  (held)      : ${success}   (expect ${N})`)
  console.log(`409 Conflict        : ${conflict}   (expect ${VUS - N})`)
  console.log(`Unexpected          : ${other}   (expect 0)`)
  console.log(`Hold p95            : ${d.lock_hold_duration?.values?.['p(95)']?.toFixed(1) ?? 'n/a'} ms`)
  console.log(success <= N && other === 0
    ? 'RESULT: PASS — lock prevented over-sell'
    : 'RESULT: FAIL — over-sell detected (no/broken lock)')
  console.log('==========================================\n')

  return { 'results/redis-lock-summary.json': JSON.stringify(data, null, 2) }
}
