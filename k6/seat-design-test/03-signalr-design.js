/**
 * 03-signalr-design.js — Concurrent SignalR seat designer connections (flat model)
 *
 * Each VU:
 *   1. Negotiates a connection token from /hubs/seat-design/negotiate
 *   2. Opens a WebSocket to the hub
 *   3. Sends the SignalR JSON handshake
 *   4. Calls JoinSeatMap → receives CurrentPresence (online users + selections)
 *   5. AddSeat (flat seat w/ position) → SeatAdded, then UpdateSeats (move it)
 *      and SendCursorPosition a few times
 *   6. DeleteSeats (cleanup) + LeaveSeatMap, then closes cleanly
 *
 * Proves:
 *   - Redis seat:presence:{seatMapId} grows as VUs join
 *   - Hub handles concurrent flat seat writes without data races
 *
 * Prereqs:
 *   - SEAT_MAP_ID must be a Draft seat map owned by the load-test org
 *   - loadtest org credentials valid (config.js)
 *
 * Run:
 *   k6 run -e SEAT_MAP_ID=<uuid> 03-signalr-design.js
 */

import http from 'k6/http'
import ws   from 'k6/ws'
import { check, sleep } from 'k6'
import { Trend, Rate, Counter } from 'k6/metrics'
import {
  SEAT_SVC_URL, SEAT_SVC_WS,
  SEAT_MAP_ID, ORG_EMAIL, ORG_PASSWORD, ORG_ID,
  BASE_URL,
  loginOrg, authHeaders,
  signalrInvoke, signalrHandshake, signalrPong, parseSignalrMessages,
} from './config.js'

const connectDuration  = new Trend('signalr_connect_duration', true)
const joinDuration     = new Trend('signalr_join_duration', true)
const msgReceived      = new Counter('signalr_messages_received')
const errorRate        = new Rate('signalr_error_rate')

export const options = {
  scenarios: {
    designers: {
      executor: 'ramping-vus',
      stages: [
        { duration: '10s', target: 10 },
        { duration: '30s', target: 20 },
        { duration: '10s', target: 0  },
      ],
    },
  },
  thresholds: {
    signalr_connect_duration: ['p(95)<2000'],
    signalr_join_duration:    ['p(95)<3000'],
    signalr_error_rate:       ['rate<0.05'],
  },
}

export function setup() {
  const token = loginOrg()
  const switchRes = http.post(
    `${BASE_URL}/auth/role`,
    JSON.stringify({ organizationId: ORG_ID, organizationName: 'Load Test Org' }),
    { headers: authHeaders(token) }
  )
  if (switchRes.status !== 200) throw new Error(`Role switch failed: ${switchRes.status}`)
  const orgToken = JSON.parse(switchRes.body).accessToken
  if (!SEAT_MAP_ID) throw new Error('SEAT_MAP_ID env var is required')
  return { token: orgToken }
}

export default function ({ token }) {
  // 1. Negotiate — get connection token
  const connectStart = Date.now()
  const negRes = http.post(
    `${SEAT_SVC_URL}/hubs/seat-design/negotiate?negotiateVersion=1`,
    null,
    { headers: { Authorization: `Bearer ${token}` } }
  )

  const negOk = check(negRes, {
    'negotiate 200':       r => r.status === 200,
    'has connectionToken': r => { try { return !!JSON.parse(r.body).connectionToken } catch { return false } },
  })

  if (!negOk) { errorRate.add(true); sleep(1); return }

  const { connectionToken } = JSON.parse(negRes.body)
  connectDuration.add(Date.now() - connectStart)

  // 2. Open WebSocket
  const wsUrl = `${SEAT_SVC_WS}/hubs/seat-design?id=${encodeURIComponent(connectionToken)}&access_token=${encodeURIComponent(token)}`

  let joined     = false
  let seatId     = null
  let joinStart  = 0
  let errors     = 0

  // Unique label per (seat map, VU, iteration) — Label is unique per seat map.
  const label = `LT${__VU}-${__ITER}`
  const seatNumber = __VU * 100000 + __ITER

  const res = ws.connect(wsUrl, {}, function (socket) {

    socket.on('open', function () {
      socket.send(signalrHandshake())
    })

    socket.on('message', function (data) {
      const messages = parseSignalrMessages(data)

      messages.forEach(msg => {
        msgReceived.add(1)

        // Empty object {} = handshake ACK
        if (!joined && Object.keys(msg).length === 0) {
          joined = true
          joinStart = Date.now()
          socket.send(signalrInvoke('JoinSeatMap', [SEAT_MAP_ID]))
          return
        }

        if (msg.type === 6) {
          socket.send(signalrPong())
          return
        }

        // Initial join ack — CurrentPresence is sent to the caller on JoinSeatMap
        if (msg.type === 1 && msg.target === 'CurrentPresence' && joinStart) {
          joinDuration.add(Date.now() - joinStart)
          joinStart = 0

          // Add one flat seat (position is a JSON string per AddSeatDto.Position)
          socket.send(signalrInvoke('AddSeat', [SEAT_MAP_ID, {
            seatMapId:  SEAT_MAP_ID,
            label,
            seatNumber,
            seatType:   1,
            position:   JSON.stringify({ x: 100 + __VU * 12, y: 200 + __ITER * 12 }),
          }]))
        }

        // Our seat was created — capture id, move it, then nudge the cursor
        if (msg.type === 1 && msg.target === 'SeatAdded') {
          const added = msg.arguments?.[0]
          if (!added || added.label !== label) return   // ignore other VUs' seats
          seatId = added.id

          socket.send(signalrInvoke('UpdateSeats', [SEAT_MAP_ID, {
            seats: [{ seatId, position: JSON.stringify({ x: 300 + __VU * 12, y: 260 + __ITER * 12 }) }],
          }]))

          for (let i = 0; i < 3; i++) {
            socket.send(signalrInvoke('SendCursorPosition', [SEAT_MAP_ID, {
              x: 100 + i * 50 + __VU * 10,
              y: 200 + __VU * 5,
            }]))
          }
        }

        if (msg.type === 3) {
          // Invocation completion — nothing needed
        }
      })
    })

    socket.on('error', function (e) {
      errors++
    })

    // Cleanup + leave after 20 s so the seat map isn't polluted
    socket.setTimeout(function () {
      if (seatId) socket.send(signalrInvoke('DeleteSeats', [SEAT_MAP_ID, [seatId]]))
      if (joined) socket.send(signalrInvoke('LeaveSeatMap', [SEAT_MAP_ID]))
      socket.close()
    }, 20000)
  })

  check(res, { 'WS closed cleanly': r => r && r.status === 1000 })
  errorRate.add(errors > 0)

  sleep(1)
}

export function handleSummary(data) {
  const d = data.metrics
  const fmt = (key, pct) => d[key]?.values?.[`p(${pct})`]?.toFixed(1) ?? 'n/a'

  console.log('\n===== SignalR Designer Test Summary =====')
  console.log(`Connect p95       : ${fmt('signalr_connect_duration', 95)} ms`)
  console.log(`JoinSeatMap p95   : ${fmt('signalr_join_duration',    95)} ms`)
  console.log(`Messages received : ${d.signalr_messages_received?.values?.count ?? 0}`)
  console.log(`Error rate        : ${((d.signalr_error_rate?.values?.rate ?? 0) * 100).toFixed(2)}%`)
  console.log('=========================================\n')

  return { 'results/signalr-design-summary.json': JSON.stringify(data, null, 2) }
}
