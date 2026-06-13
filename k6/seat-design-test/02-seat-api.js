/**
 * 02-seat-api.js — Seat map REST API throughput test
 *
 * Simulates org users creating and reading seat maps concurrently.
 * Each VU runs a full create → read → stats → delete cycle, so the DB
 * is not polluted after the test.
 *
 * Run:
 *   k6 run -e ORG_EMAIL=loadtest@eventiq.dev -e ORG_PASSWORD=LoadTest@123 02-seat-api.js
 */

import http from 'k6/http'
import { check, sleep } from 'k6'
import { Trend, Rate } from 'k6/metrics'
import { BASE_URL, EVENT_ID, ORG_ID, loginOrg, authHeaders } from './config.js'

const createDuration = new Trend('seatmap_create_duration', true)
const readDuration   = new Trend('seatmap_read_duration', true)
const statsDuration  = new Trend('seatmap_stats_duration', true)
const errorRate      = new Rate('seatmap_error_rate')

export const options = {
  scenarios: {
    steady: {
      executor: 'ramping-vus',
      stages: [
        { duration: '10s', target: 20 },
        { duration: '30s', target: 50 },
        { duration: '10s', target: 0  },
      ],
    },
  },
  thresholds: {
    http_req_duration:      ['p(95)<800', 'p(99)<1500'],
    seatmap_create_duration:['p(95)<600'],
    seatmap_read_duration:  ['p(95)<300'],
    seatmap_stats_duration: ['p(95)<300'],
    seatmap_error_rate:     ['rate<0.02'],
    http_req_failed:        ['rate<0.02'],
  },
}

export function setup() {
  const token = loginOrg()
  // Switch to org role
  const switchRes = http.post(
    `${BASE_URL}/auth/role`,
    JSON.stringify({ organizationId: ORG_ID, organizationName: 'Load Test Org' }),
    { headers: authHeaders(token) }
  )
  if (switchRes.status !== 200) throw new Error(`Role switch failed: ${switchRes.status}`)
  return { token: JSON.parse(switchRes.body).accessToken }
}

export default function ({ token }) {
  const headers = authHeaders(token)
  let seatMapId = null

  // --- Create ---
  const createRes = http.post(
    `${BASE_URL}/seat-maps`,
    JSON.stringify({
      eventId: EVENT_ID,
      chartId: `c${__VU.toString().padStart(7, '0')}-0000-0000-0000-000000000000`,
      name: `LoadTest-VU${__VU}-iter${__ITER}`,
    }),
    { headers, tags: { op: 'create' } }
  )

  const createOk = check(createRes, {
    'create 200/201': r => r.status === 200 || r.status === 201,
    'has id':         r => { try { return !!JSON.parse(r.body).id } catch { return false } },
  })
  createDuration.add(createRes.timings.duration)
  errorRate.add(!createOk)

  if (!createOk) { sleep(1); return }
  seatMapId = JSON.parse(createRes.body).id

  sleep(0.2)

  // --- Read metadata (objects + bounds, no seats) ---
  const readRes = http.get(
    `${BASE_URL}/seat-maps/${seatMapId}`,
    { headers, tags: { op: 'read' } }
  )
  const readOk = check(readRes, {
    'read 200':       r => r.status === 200,
    'has objects':    r => { try { return Array.isArray(JSON.parse(r.body).objects) } catch { return false } },
    'has fullBbox':   r => { try { return JSON.parse(r.body).fullBbox != null } catch { return false } },
  })
  readDuration.add(readRes.timings.duration)
  errorRate.add(!readOk)

  sleep(0.2)

  // --- Read all seats (dedicated design endpoint) ---
  const seatsRes = http.get(
    `${BASE_URL}/seat-maps/${seatMapId}/seats`,
    { headers, tags: { op: 'seats' } }
  )
  const seatsOk = check(seatsRes, {
    'seats 200':      r => r.status === 200,
    'seats is array': r => { try { return Array.isArray(JSON.parse(r.body)) } catch { return false } },
  })
  errorRate.add(!seatsOk)

  sleep(0.2)

  // --- Stats ---
  const statsRes = http.get(
    `${BASE_URL}/seat-maps/${seatMapId}/stats`,
    { headers, tags: { op: 'stats' } }
  )
  const statsOk = check(statsRes, {
    'stats 200':       r => r.status === 200,
    'totalSeats >= 0': r => { try { return JSON.parse(r.body).totalSeats >= 0 } catch { return false } },
  })
  statsDuration.add(statsRes.timings.duration)
  errorRate.add(!statsOk)

  sleep(0.2)

  // --- List by event ---
  const listRes = http.get(
    `${BASE_URL}/seat-maps?eventId=${EVENT_ID}`,
    { headers, tags: { op: 'list' } }
  )
  check(listRes, { 'list 200': r => r.status === 200 })

  sleep(0.2)

  // --- Delete (cleanup) ---
  if (seatMapId) {
    const delRes = http.del(
      `${BASE_URL}/seat-maps/${seatMapId}`,
      null,
      { headers, tags: { op: 'delete' } }
    )
    check(delRes, { 'delete 200/204': r => r.status === 200 || r.status === 204 })
  }

  sleep(0.5)
}

export function handleSummary(data) {
  const d = data.metrics
  const fmt = (key, pct) => d[key]?.values?.[`p(${pct})`]?.toFixed(1) ?? 'n/a'

  console.log('\n===== Seat Map API Test Summary =====')
  console.log(`Create p95    : ${fmt('seatmap_create_duration', 95)} ms`)
  console.log(`Read   p95    : ${fmt('seatmap_read_duration',   95)} ms`)
  console.log(`Stats  p95    : ${fmt('seatmap_stats_duration',  95)} ms`)
  console.log(`Error rate    : ${((d.seatmap_error_rate?.values?.rate ?? 0) * 100).toFixed(2)}%`)
  console.log('=====================================\n')

  return { 'results/seat-api-summary.json': JSON.stringify(data, null, 2) }
}
