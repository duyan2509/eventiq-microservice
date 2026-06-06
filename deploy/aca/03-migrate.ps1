# =============================================================================
# 03-migrate.ps1 — apply EF Core migrations to the Azure PostgreSQL server.
#
# Runs `dotnet ef database update` for each service against $PG_CONN. All five
# services share one database; each writes into its own schema.
#
# Run from a machine that can reach Azure Postgres (the AllowAzure firewall
# rule covers Azure egress; for your laptop also add your public IP:
#   az postgres flexible-server firewall-rule create -g $RG --name $PG_SERVER \
#     --rule-name MyIp --start-ip-address <ip> --end-ip-address <ip> )
# =============================================================================
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\config.ps1"

if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) {
  Write-Host "Installing dotnet-ef global tool..." -ForegroundColor Yellow
  dotnet tool install --global dotnet-ef
}

# EF reads these from configuration via env vars (double underscore = nesting).
# Production env so the host registers Azure Service Bus (conn string present);
# the bus is never started during a design-time migration.
$env:ASPNETCORE_ENVIRONMENT          = "Production"
$env:ConnectionStrings__Postgres     = $PG_CONN
$env:AzureServiceBus__ConnectionString = $SB_CONN

$projects = @(
  "Eventiq.UserService",
  "Eventiq.OrganizationService",
  "Eventiq.EventService",
  "Eventiq.SeatService",
  "Eventiq.PaymentService"
)

Push-Location $REPO_ROOT
try {
  foreach ($p in $projects) {
    Write-Host "==> dotnet ef database update -p $p" -ForegroundColor Cyan
    dotnet ef database update --project $p --startup-project $p
  }
}
finally { Pop-Location }

Write-Host "All migrations applied to $PG_HOST/$PG_DB" -ForegroundColor Green
