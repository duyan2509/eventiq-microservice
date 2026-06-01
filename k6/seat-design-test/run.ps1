# run.ps1 — Run all seat-design k6 tests in sequence
#
# Prerequisites:
#   1. Install k6: https://k6.io/docs/get-started/installation/
#   2. Run backend: cd eventiq-microservice && .\run-all.ps1
#   3. Seed DB:
#      psql "<neon-connection-string>" \
#        -v load_test_user_id="'<uuid-of-loadtest-user>'" \
#        -f ..\..\data\seed_load_test_data.sql
#   4. Register loadtest@eventiq.dev, assign Org role, approve event
#      (or run the seed SQL with --all steps first)
#
# Env vars to set (required):
#   TOKEN       — org-role JWT for the load test user
#   SESSION_ID  — a session ID that has a linked seat map (from event approval)
#   SEAT_MAP_ID — a Draft/Published seat map ID (for SignalR test)
#
# Usage:
#   $env:TOKEN      = "eyJ..."
#   $env:SESSION_ID = "xxxx-..."
#   $env:SEAT_MAP_ID= "yyyy-..."
#   .\run.ps1
#
# Or pass inline:
#   .\run.ps1 -Token "eyJ..." -SessionId "xxxx-..." -SeatMapId "yyyy-..."

param(
  [string]$Token      = $env:TOKEN,
  [string]$SessionId  = $env:SESSION_ID,
  [string]$SeatMapId  = $env:SEAT_MAP_ID,
  [string]$BaseUrl    = 'http://localhost:5001/gateway',
  [string]$SeatSvcUrl = 'http://localhost:5234',
  [string]$SeatSvcWs  = 'ws://localhost:5234'
)

Set-Location $PSScriptRoot
New-Item -ItemType Directory -Force -Path results | Out-Null

function Run-K6 {
  param([string]$Script, [hashtable]$Env)

  $envArgs = $Env.GetEnumerator() | ForEach-Object { "-e", "$($_.Key)=$($_.Value)" }
  Write-Host "`n>> Running $Script" -ForegroundColor Cyan

  k6 run @envArgs $Script
  if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED: $Script (exit $LASTEXITCODE)" -ForegroundColor Red
  } else {
    Write-Host "PASSED: $Script" -ForegroundColor Green
  }
}

$common = @{
  BASE_URL     = $BaseUrl
  SEAT_SVC_URL = $SeatSvcUrl
  SEAT_SVC_WS  = $SeatSvcWs
  TOKEN        = $Token
}

# 01 — Layout meta cache
if ($SessionId) {
  Run-K6 '01-layout-cache.js' ($common + @{ SESSION_ID = $SessionId })
} else {
  Write-Host "Skipping 01-layout-cache.js — SESSION_ID not set" -ForegroundColor Yellow
}

# 02 — Seat API CRUD
Run-K6 '02-seat-api.js' $common

# 03 — SignalR concurrent designers
if ($SeatMapId) {
  Run-K6 '03-signalr-design.js' ($common + @{ SEAT_MAP_ID = $SeatMapId })
} else {
  Write-Host "Skipping 03-signalr-design.js — SEAT_MAP_ID not set" -ForegroundColor Yellow
}

# 04 — Viewport vs get-all comparison
if ($SessionId) {
  Run-K6 '04-viewport-compare.js' ($common + @{ SESSION_ID = $SessionId })
} else {
  Write-Host "Skipping 04-viewport-compare.js — SESSION_ID not set" -ForegroundColor Yellow
}

Write-Host "`nAll tests complete. JSON summaries in ./results/" -ForegroundColor Cyan
