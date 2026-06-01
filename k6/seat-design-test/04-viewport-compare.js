/**
 * 04-viewport-compare.js — Get-all vs viewport-based seat loading
 *
 * Quantifies the viewport refactor: compares the booking seats endpoint when
 * fetching the WHOLE map versus a single VIEWPORT chunk (bounding box).
 *
 *   - get_all  : GET /seat-maps/sessions/{id}/seats              (every seat)
 *   - viewport : GET /seat-maps/sessions/{id}/seats?x1&y1&x2&y2  (top-left quadrant)
 *
 * Reports p95 latency and average response size for each, so the thesis can
 * show payload/latency reduction (most meaningful on large venues, ~5000 seats).
 * The two scenarios run back-to-back (staggered startTime) for a clean compare.
 *
 * Prereqs:
 *   - Seed DB + approve event so the session has a published seat map with seats
 *   - Set SESSION_ID env var
 *
 * Run:
 *   k6 run -e SESSION_ID=<uuid> 04-viewport-compare.js
 */

import http from 'k6/http'
import { check, sleep } from 'k6'
import { Trend, Rate, Counter } from 'k6/metrics'
import { BASE_URL, SESSION_ID, loginOrg, authHeaders } from './config.js'

const allDuration   = new Trend('seats_all_duration', true)
const vpDuration    = new Trend('seats_viewport_duration', true)
const allBytes      = new Trend('seats_all_bytes')
const vpBytes       = new Trend('seats_viewport_bytes')
const errorRate     = new Rate('seats_error_rate')
const reqs          = new Counter('seats_total_requests')

export const options = {
  scenarios: {
    get_all: {
      executor: 'ramping-vus',
      exec: 'getAll',
      startTime: '0s',
      stages: [
        { duration: '10s', target: 50 },
        { duration: '15s', target: 50 },
        { duration: '5s',  target: 0  },
      ],
      tags: { mode: 'get_all' },
    },
    viewport: {
      executor: 'ramping-vus',
      exec: 'viewport',
      startTime: '35s',          // after get_all finishes
      stages: [
        { duration: '10s', target: 50 },
        { duration: '15s', target: 50 },
        { duration: '5s',  target: 0  },
      ],
      tags: { mode: 'viewport' },
    },
  },
  thresholds: {
    http_req_failed:          ['rate<0.01'],
    seats_error_rate:         ['rate<0.01'],
    seats_viewport_duration:  ['p(95)<200'],
  },
}

export function setup() {
  if (!SESSION_ID) throw new Error('SESSION_ID env var is required')
  const token = loginOrg()

  // Read meta to derive a viewport bbox = top-left quadrant of the full map.
  const metaRes = http.get(`${BASE_URL}/seat-maps/sessions/${SESSION_ID}/meta`, {
    headers: authHeaders(token),
  })
  if (metaRes.status !== 200) throw new Error(`Meta request failed: ${metaRes.status}`)
  const fb = JSON.parse(metaRes.body).fullBbox || { x1: 0, y1: 0, x2: 1000, y2: 1000 }
  const vp = {
    x1: fb.x1,
    y1: fb.y1,
    x2: fb.x1 + (fb.x2 - fb.x1) / 2,
    y2: fb.y1 + (fb.y2 - fb.y1) / 2,
  }
  return { token, vp }
}

export function getAll({ token }) {
  const res = http.get(`${BASE_URL}/seat-maps/sessions/${SESSION_ID}/seats`, {
    headers: { Authorization: `Bearer ${token}` },
    tags: { name: 'seats_all' },
  })
  record(res, allDuration, allBytes)
  sleep(0.2)
}

export function viewport({ token, vp }) {
  const q = `x1=${vp.x1}&y1=${vp.y1}&x2=${vp.x2}&y2=${vp.y2}`
  const res = http.get(`${BASE_URL}/seat-maps/sessions/${SESSION_ID}/seats?${q}`, {
    headers: { Authorization: `Bearer ${token}` },
    tags: { name: 'seats_viewport' },
  })
  record(res, vpDuration, vpBytes)
  sleep(0.2)
}

function record(res, durTrend, bytesTrend) {
  const ok = check(res, {
    'status 200':     r => r.status === 200,
    'has seats':      r => { try { return Array.isArray(JSON.parse(r.body).seats) } catch { return false } },
  })
  durTrend.add(res.timings.duration)
  bytesTrend.add(res.body ? res.body.length : 0)
  errorRate.add(!ok)
  reqs.add(1)
}

export function handleSummary(data) {
  const d = data.metrics
  const p95 = k => d[k]?.values?.['p(95)']?.toFixed(1) ?? 'n/a'
  const avg = k => d[k]?.values?.avg?.toFixed(0) ?? 'n/a'

  console.log('\n===== Viewport vs Get-All Summary =====')
  console.log(`get_all   p95 : ${p95('seats_all_duration')} ms   avg body : ${avg('seats_all_bytes')} bytes`)
  console.log(`viewport  p95 : ${p95('seats_viewport_duration')} ms   avg body : ${avg('seats_viewport_bytes')} bytes`)
  console.log(`Error rate    : ${((d.seats_error_rate?.values?.rate ?? 0) * 100).toFixed(2)}%`)
  console.log('=======================================\n')

  return { 'results/viewport-compare-summary.json': JSON.stringify(data, null, 2) }
}
