// ---------------------------------------------------------------------------
// Shared config — override any value via k6 env vars (-e KEY=VALUE)
// ---------------------------------------------------------------------------

export const BASE_URL        = __ENV.BASE_URL        || 'http://localhost:5001/gateway'
export const SEAT_SVC_URL    = __ENV.SEAT_SVC_URL    || 'http://localhost:5234'
export const SEAT_SVC_WS     = __ENV.SEAT_SVC_WS     || 'ws://localhost:5234'

// Fixed load-test IDs (matches seed_load_test_data.sql)
export const ORG_ID          = __ENV.ORG_ID          || 'a0000000-0000-0000-0000-000000000001'
export const EVENT_ID        = __ENV.EVENT_ID        || 'b0000000-0000-0000-0000-000000000001'

// Defaults match the fixed IDs created by data/seed_load_test_data.sql
export const SESSION_ID      = __ENV.SESSION_ID      || 'e0000000-0000-0000-0000-0000000000aa'  // published session clone (01, 04)
export const SEAT_MAP_ID     = __ENV.SEAT_MAP_ID     || 'd0000000-0000-0000-0000-0000000000cc'  // draft map for SignalR (03)

// Credentials for the load-test org user
export const ORG_EMAIL       = __ENV.ORG_EMAIL       || 'loadtest@eventiq.dev'
export const ORG_PASSWORD    = __ENV.ORG_PASSWORD    || 'LoadTest@123'
export const ADMIN_EMAIL     = __ENV.ADMIN_EMAIL     || 'eventiq@gmail.com'
export const ADMIN_PASSWORD  = __ENV.ADMIN_PASSWORD  || 'Admin@123'

// SignalR protocol separator
export const SIGNALR_SEP = '\x1e'

// ---------------------------------------------------------------------------
// Auth helpers
// ---------------------------------------------------------------------------
import http from 'k6/http'

export function loginOrg() {
  const res = http.post(`${BASE_URL}/auth/login`, JSON.stringify({
    email: ORG_EMAIL, password: ORG_PASSWORD,
  }), { headers: { 'Content-Type': 'application/json' } })

  if (res.status !== 200) throw new Error(`Org login failed: ${res.status} ${res.body}`)
  return JSON.parse(res.body).accessToken
}

export function loginAdmin() {
  const res = http.post(`${BASE_URL}/auth/login`, JSON.stringify({
    email: ADMIN_EMAIL, password: ADMIN_PASSWORD,
  }), { headers: { 'Content-Type': 'application/json' } })

  if (res.status !== 200) throw new Error(`Admin login failed: ${res.status} ${res.body}`)
  return JSON.parse(res.body).accessToken
}

export function authHeaders(token) {
  return { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }
}

// ---------------------------------------------------------------------------
// SignalR helpers
// ---------------------------------------------------------------------------
export function signalrInvoke(target, args) {
  return JSON.stringify({ type: 1, invocationId: String(Date.now()), target, arguments: args }) + SIGNALR_SEP
}

export function signalrHandshake() {
  return `{"protocol":"json","version":1}${SIGNALR_SEP}`
}

export function signalrPong() {
  return JSON.stringify({ type: 6 }) + SIGNALR_SEP
}

export function parseSignalrMessages(raw) {
  return raw.split(SIGNALR_SEP).filter(s => s.trim().length > 0).map(s => {
    try { return JSON.parse(s) } catch { return null }
  }).filter(Boolean)
}

// Negotiate and return the connectionToken for WebSocket upgrade
export function negotiateHub(hubPath, token) {
  const res = http.post(
    `${SEAT_SVC_URL}${hubPath}/negotiate?negotiateVersion=1`,
    null,
    { headers: { Authorization: `Bearer ${token}` } }
  )
  if (res.status !== 200) throw new Error(`Negotiate failed: ${res.status}`)
  return JSON.parse(res.body).connectionToken
}
