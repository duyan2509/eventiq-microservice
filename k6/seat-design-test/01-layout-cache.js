/**
 * 01-layout-cache.js — Booking layout-meta output cache throughput test
 *
 * Proves Redis output cache is working on the booking metadata endpoint
 * (GET /seat-maps/sessions/{id}/meta): the first request (cache MISS) is
 * slower; subsequent requests from 100 concurrent VUs are served from Redis
 * in < 200 ms p95. Seats themselves are streamed separately by viewport
 * (see 04-viewport-compare.js).
 *
 * Prereqs:
 *   - Seed DB (data/seed_load_test_data.sql)
 *   - Approve the event so per-session seat maps are cloned
 *   - Set SESSION_ID env var to any session that has a linked seat map
 *
 * Run:
 *   k6 run -e SESSION_ID=<uuid> 01-layout-cache.js
 */

import http from 'k6/http'
import { check, sleep } from 'k6'
import { Trend, Rate, Counter } from 'k6/metrics'
import { BASE_URL, SESSION_ID, loginOrg, authHeaders } from './config.js'

const layoutDuration  = new Trend('layout_req_duration', true)
const cacheErrorRate  = new Rate('layout_error_rate')
const totalRequests   = new Counter('layout_total_requests')

export const options = {
  scenarios: {
    // Single VU warms the cache before the load phase starts
    cache_warmup: {
      executor: 'constant-vus',
      vus: 1,
      duration: '5s',
      tags: { phase: 'warmup' },
    },
    // Ramp to 100 VUs — all hitting the same session (same cache key)
    cache_load: {
      executor: 'ramping-vus',
      startTime: '6s',
      stages: [
        { duration: '15s', target: 50  },
        { duration: '30s', target: 100 },
        { duration: '10s', target: 0   },
      ],
      tags: { phase: 'load' },
    },
  },
  thresholds: {
    http_req_duration:    ['p(95)<300', 'p(99)<500'],
    layout_req_duration:  ['p(95)<200'],
    layout_error_rate:    ['rate<0.01'],
    http_req_failed:      ['rate<0.01'],
  },
}

export function setup() {
  if (!SESSION_ID) throw new Error('SESSION_ID env var is required')
  const token = loginOrg()
  // Warm request — confirms endpoint reachable and primes the cache
  const res = http.get(`${BASE_URL}/seat-maps/sessions/${SESSION_ID}/meta`, {
    headers: authHeaders(token),
  })
  if (res.status !== 200) throw new Error(`Warmup request failed: ${res.status} — is the session linked to a published seat map?`)
  return { token }
}

export default function ({ token }) {
  const res = http.get(`${BASE_URL}/seat-maps/sessions/${SESSION_ID}/meta`, {
    headers: { Authorization: `Bearer ${token}` },
    tags: { name: 'GET_meta' },
  })

  const ok = check(res, {
    'status 200':     r => r.status === 200,
    'has objects':    r => { try { return Array.isArray(JSON.parse(r.body).objects) } catch { return false } },
    'has fullBbox':   r => { try { return JSON.parse(r.body).fullBbox != null } catch { return false } },
    'response < 1s':  r => r.timings.duration < 1000,
  })

  layoutDuration.add(res.timings.duration)
  cacheErrorRate.add(!ok)
  totalRequests.add(1)

  sleep(0.2)
}

export function handleSummary(data) {
  const d = data.metrics
  const p95 = d.layout_req_duration?.values?.['p(95)']?.toFixed(1) ?? 'n/a'
  const p99 = d.layout_req_duration?.values?.['p(99)']?.toFixed(1) ?? 'n/a'
  const errRate = ((d.layout_error_rate?.values?.rate ?? 0) * 100).toFixed(2)
  const reqs = d.layout_total_requests?.values?.count ?? 0

  console.log('\n===== Layout Meta Cache Test Summary =====')
  console.log(`Total requests : ${reqs}`)
  console.log(`p95 latency    : ${p95} ms`)
  console.log(`p99 latency    : ${p99} ms`)
  console.log(`Error rate     : ${errRate}%`)
  console.log('=====================================\n')

  return { 'results/layout-cache-summary.json': JSON.stringify(data, null, 2) }
}
