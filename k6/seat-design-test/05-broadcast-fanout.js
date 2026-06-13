/**
 * 05-broadcast-fanout.js — Realtime SignalR broadcast fanout latency
 *
 * The collaboration money-metric: 1 EDITOR + N OBSERVERS all on the SAME seat map.
 * The editor emits cursor moves carrying a wall-clock timestamp in X; every observer
 * receives the `CursorMoved` broadcast (Clients.OthersInGroup) and records
 *   fanout = Date.now() - X   (ms from editor send to observer receive).
 * k6 runs in ONE process so Date.now() is a shared clock across all VUs — the
 * editor's embedded timestamp and the observer's receive time are directly comparable.
 *
 * Proves how broadcast latency scales with the number of concurrent collaborators.
 * Run at several N to plot fanout p95 vs observers:
 *   k6 run -e OBSERVERS=10  05-broadcast-fanout.js
 *   k6 run -e OBSERVERS=50  05-broadcast-fanout.js
 *   k6 run -e OBSERVERS=100 05-broadcast-fanout.js
 *
 * Prereqs: SEAT_MAP_ID = a Draft seat map owned by the load-test org (config.js).
 */

import http from 'k6/http'
import ws from 'k6/ws'
import { check } from 'k6'
import { Trend, Counter, Rate } from 'k6/metrics'
import {
  SEAT_SVC_URL, SEAT_SVC_WS, SEAT_MAP_ID, ORG_ID, BASE_URL,
  loginOrg, authHeaders, signalrInvoke, signalrHandshake, signalrPong, parseSignalrMessages,
} from './config.js'

const OBSERVERS        = Number(__ENV.OBSERVERS || 50)
const DURATION         = __ENV.DURATION || '40s'
const EDIT_INTERVAL_MS = Number(__ENV.EDIT_INTERVAL_MS || 500)
// Observers join first; editor starts after this so the group is fully populated.
const EDITOR_START     = __ENV.EDITOR_START || '6s'
const HOLD_MS          = Number(__ENV.HOLD_MS || 32000)   // how long each socket stays open

const fanout    = new Trend('broadcast_fanout_ms', true)
const received  = new Counter('broadcast_received')
const sent      = new Counter('broadcast_sent')
const joinRate  = new Rate('broadcast_join_ok')
const errorRate = new Rate('broadcast_error_rate')

export const options = {
  scenarios: {
    observers: { executor: 'constant-vus', vus: OBSERVERS, duration: DURATION, exec: 'observer' },
    editor:    { executor: 'constant-vus', vus: 1, duration: DURATION, exec: 'editor', startTime: EDITOR_START },
  },
  thresholds: {
    broadcast_fanout_ms:  ['p(95)<1500'],
    broadcast_error_rate: ['rate<0.05'],
    broadcast_join_ok:    ['rate>0.95'],
  },
}

export function setup() {
  const token = loginOrg()
  const sw = http.post(
    `${BASE_URL}/auth/role`,
    JSON.stringify({ organizationId: ORG_ID, organizationName: 'Load Test Org' }),
    { headers: authHeaders(token) }
  )
  const orgToken = sw.status === 200 ? JSON.parse(sw.body).accessToken : token
  if (!SEAT_MAP_ID) throw new Error('SEAT_MAP_ID env var is required')
  return { token: orgToken }
}

function negotiate(token) {
  const neg = http.post(
    `${SEAT_SVC_URL}/hubs/seat-design/negotiate?negotiateVersion=1`,
    null,
    { headers: { Authorization: `Bearer ${token}` } }
  )
  if (neg.status !== 200) { errorRate.add(true); return null }
  try { return JSON.parse(neg.body).connectionToken } catch { errorRate.add(true); return null }
}

// An OBSERVER joins the map and only listens, timing every CursorMoved broadcast.
export function observer({ token }) {
  const ct = negotiate(token)
  if (!ct) return
  const wsUrl = `${SEAT_SVC_WS}/hubs/seat-design?id=${encodeURIComponent(ct)}&access_token=${encodeURIComponent(token)}`
  let joined = false

  ws.connect(wsUrl, {}, function (socket) {
    socket.on('open', () => socket.send(signalrHandshake()))

    socket.on('message', function (data) {
      parseSignalrMessages(data).forEach(msg => {
        if (!joined && Object.keys(msg).length === 0) {     // handshake ack
          joined = true
          socket.send(signalrInvoke('JoinSeatMap', [SEAT_MAP_ID]))
          return
        }
        if (msg.type === 6) { socket.send(signalrPong()); return }
        if (msg.type === 1 && msg.target === 'CurrentPresence') { joinRate.add(true); return }
        if (msg.type === 1 && msg.target === 'CursorMoved') {
          const ts = msg.arguments?.[0]?.x
          if (typeof ts === 'number' && ts > 1e12) {         // looks like an epoch-ms stamp
            fanout.add(Date.now() - ts)
            received.add(1)
          }
        }
      })
    })

    socket.on('error', () => errorRate.add(true))
    socket.setTimeout(() => { if (joined) socket.send(signalrInvoke('LeaveSeatMap', [SEAT_MAP_ID])); socket.close() }, HOLD_MS)
  })
}

// The single EDITOR joins the map and emits a timestamped cursor move on an interval.
export function editor({ token }) {
  const ct = negotiate(token)
  if (!ct) return
  const wsUrl = `${SEAT_SVC_WS}/hubs/seat-design?id=${encodeURIComponent(ct)}&access_token=${encodeURIComponent(token)}`
  let joined = false

  ws.connect(wsUrl, {}, function (socket) {
    socket.on('open', () => socket.send(signalrHandshake()))

    socket.on('message', function (data) {
      parseSignalrMessages(data).forEach(msg => {
        if (!joined && Object.keys(msg).length === 0) {
          joined = true
          socket.send(signalrInvoke('JoinSeatMap', [SEAT_MAP_ID]))
          return
        }
        if (msg.type === 6) { socket.send(signalrPong()); return }
        if (msg.type === 1 && msg.target === 'CurrentPresence') {
          // Start emitting timestamped cursor moves; X carries Date.now().
          socket.setInterval(function () {
            socket.send(signalrInvoke('SendCursorPosition', [SEAT_MAP_ID, { x: Date.now(), y: 1 }]))
            sent.add(1)
          }, EDIT_INTERVAL_MS)
        }
      })
    })

    socket.on('error', () => errorRate.add(true))
    socket.setTimeout(() => { socket.send(signalrInvoke('LeaveSeatMap', [SEAT_MAP_ID])); socket.close() }, HOLD_MS)
  })
}

export function handleSummary(data) {
  const m = data.metrics
  const g = (name, stat) => (m[name] && m[name].values && m[name].values[stat] != null) ? m[name].values[stat] : 0
  const line = s => console.log(s)
  line('\n===== Broadcast Fanout Summary =====')
  line(`Observers (N)     : ${OBSERVERS}`)
  line(`Edits sent        : ${g('broadcast_sent', 'count')}`)
  line(`Broadcasts recv   : ${g('broadcast_received', 'count')}  (~expect sent * N)`)
  line(`Fanout p50        : ${g('broadcast_fanout_ms', 'p(50)').toFixed(1)} ms`)
  line(`Fanout p95        : ${g('broadcast_fanout_ms', 'p(95)').toFixed(1)} ms`)
  line(`Fanout p99        : ${g('broadcast_fanout_ms', 'p(99)').toFixed(1)} ms`)
  line(`Fanout max        : ${g('broadcast_fanout_ms', 'max').toFixed(1)} ms`)
  line(`Join OK rate      : ${(g('broadcast_join_ok', 'rate') * 100).toFixed(1)} %`)
  line(`Error rate        : ${(g('broadcast_error_rate', 'rate') * 100).toFixed(2)} %`)
  line('====================================\n')
  return { [`results/fanout-N${OBSERVERS}.json`]: JSON.stringify(data, null, 2) }
}
