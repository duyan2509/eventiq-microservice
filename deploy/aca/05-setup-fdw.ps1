# =============================================================================
# 05-setup-fdw.ps1 — wire postgres_fdw federation into analytics_db.
#
# Imports the 5 service schemas (user_service, org_service, event_service,
# seat_service, payment_service) from their per-service databases into the
# central analytics_db as foreign tables, keeping the SAME schema names. That
# lets AnalyticsService run cross-service Text2SQL JOINs in prod (FDW) mode with
# the exact same SQL it uses on Neon (single DB, native cross-schema).
#
# Run order: after 03-migrate (the source schemas must already exist), before
# you actually query analytics. All 5 databases live on the SAME flexible server,
# so every foreign server targets $PG_HOST with the admin credential.
#
# Uses `az postgres flexible-server execute` (rdbms-connect extension) so no local
# psql is required. setup_fdw.sql stays the single source of truth; this script
# just substitutes its psql :'placeholders' with the real host/credential.
# =============================================================================
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\config.ps1"

az account set --subscription $SUBSCRIPTION

Write-Host "==> Ensuring rdbms-connect CLI extension" -ForegroundColor Cyan
az extension add --name rdbms-connect --upgrade --only-show-errors 2>$null | Out-Null

# Build a concrete SQL file from the template. Single server => every *_host is
# $PG_HOST and every fdw_*_name/_pwd is the admin credential. String .Replace is
# literal (no regex), so special chars in the password are safe (avoid a single
# quote in the password though, since it terminates the SQL string literal).
$tplPath = Join-Path $REPO_ROOT "Eventiq.AnalyticsService\scripts\setup_fdw.sql"
$sql = Get-Content $tplPath -Raw
foreach ($t in 'user_host','org_host','event_host','seat_host','payment_host') {
  $sql = $sql.Replace(":'$t'", "'$PG_HOST'")
}
foreach ($t in 'fdw_user_name','fdw_org_name','fdw_event_name','fdw_seat_name','fdw_payment_name') {
  $sql = $sql.Replace(":'$t'", "'$PG_ADMIN'")
}
foreach ($t in 'fdw_user_pwd','fdw_org_pwd','fdw_event_pwd','fdw_seat_pwd','fdw_payment_pwd') {
  $sql = $sql.Replace(":'$t'", "'$PG_PASSWORD'")
}
# source database names (dbname :'user_db' etc.)
$dbTokens = @{ user_db = $PG_DB_USER; org_db = $PG_DB_ORG; event_db = $PG_DB_EVENT; seat_db = $PG_DB_SEAT; payment_db = $PG_DB_PAYMENT }
foreach ($t in $dbTokens.Keys) {
  $sql = $sql.Replace(":'$t'", "'$($dbTokens[$t])'")
}

$tmp = Join-Path $env:TEMP "setup_fdw.resolved.sql"
Set-Content -Path $tmp -Value $sql -Encoding UTF8

Write-Host "==> Applying postgres_fdw federation to $PG_DB_ANALYTICS on $PG_HOST" -ForegroundColor Cyan
az postgres flexible-server execute `
  --name $PG_SERVER `
  --admin-user $PG_ADMIN --admin-password $PG_PASSWORD `
  --database-name $PG_DB_ANALYTICS `
  --file-path $tmp --output table

Remove-Item $tmp -Force
Write-Host "FDW federation ready. AnalyticsService (ANALYTICS_MODE=prod) can now query analytics_db." -ForegroundColor Green
