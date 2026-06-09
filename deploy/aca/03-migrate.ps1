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
$env:AzureServiceBus__ConnectionString = $SB_CONN

# database-per-service: each project migrates into its own database
$projects = @(
  @{ Name = "Eventiq.UserService";         Conn = $PG_CONN_USER    },
  @{ Name = "Eventiq.OrganizationService"; Conn = $PG_CONN_ORG     },
  @{ Name = "Eventiq.EventService";        Conn = $PG_CONN_EVENT   },
  @{ Name = "Eventiq.SeatService";         Conn = $PG_CONN_SEAT    },
  @{ Name = "Eventiq.PaymentService";      Conn = $PG_CONN_PAYMENT }
)

Push-Location $REPO_ROOT
try {
  foreach ($p in $projects) {
    Write-Host "==> dotnet ef database update -p $($p.Name)" -ForegroundColor Cyan
    $env:ConnectionStrings__Postgres = $p.Conn
    dotnet ef database update --project $p.Name --startup-project $p.Name
  }
}
finally { Pop-Location }

Write-Host "All migrations applied to $PG_HOST (database-per-service)" -ForegroundColor Green
