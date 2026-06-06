# =============================================================================
# 01-provision.ps1 — create all Azure resources for the EventIQ ACA deploy.
#
#   Resource group, ACR, Log Analytics, Container Apps environment,
#   PostgreSQL Flexible Server (+db), Azure Cache for Redis, Service Bus.
#
# Idempotent-ish: re-running skips resources that already exist (az create is
# mostly idempotent). After it finishes it prints REDIS_KEY and SB_CONN —
# paste those into config.ps1 before running 03/04.
#
#   az login            # do this first (interactive)
#   .\01-provision.ps1
# =============================================================================
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\config.ps1"

az account set --subscription $SUBSCRIPTION

Write-Host "==> Registering providers + containerapp extension" -ForegroundColor Cyan
az extension add --name containerapp --upgrade --only-show-errors | Out-Null
az provider register -n Microsoft.App --wait
az provider register -n Microsoft.OperationalInsights --wait

Write-Host "==> Resource group $RG" -ForegroundColor Cyan
az group create --name $RG --location $LOCATION --only-show-errors | Out-Null

Write-Host "==> Container Registry $ACR" -ForegroundColor Cyan
az acr create --resource-group $RG --name $ACR --sku Basic --admin-enabled true --only-show-errors | Out-Null

Write-Host "==> Log Analytics workspace $LOG_WS" -ForegroundColor Cyan
az monitor log-analytics workspace create --resource-group $RG --workspace-name $LOG_WS --only-show-errors | Out-Null
$LOG_ID  = az monitor log-analytics workspace show       --resource-group $RG --workspace-name $LOG_WS --query customerId -o tsv
$LOG_KEY = az monitor log-analytics workspace get-shared-keys --resource-group $RG --workspace-name $LOG_WS --query primarySharedKey -o tsv

Write-Host "==> Container Apps environment $ACA_ENV" -ForegroundColor Cyan
az containerapp env create `
  --name $ACA_ENV --resource-group $RG --location $LOCATION `
  --logs-workspace-id $LOG_ID --logs-workspace-key $LOG_KEY --only-show-errors | Out-Null

Write-Host "==> PostgreSQL Flexible Server $PG_SERVER (B1ms)" -ForegroundColor Cyan
az postgres flexible-server create `
  --resource-group $RG --name $PG_SERVER `
  --admin-user $PG_ADMIN --admin-password $PG_PASSWORD `
  --sku-name Standard_B1ms --tier Burstable --storage-size 32 `
  --version 16 --public-access 0.0.0.0 --yes --only-show-errors | Out-Null
# Allow all Azure services (ACA egress IPs are dynamic) — 0.0.0.0 rule = "Azure services"
az postgres flexible-server firewall-rule create `
  --resource-group $RG --name $PG_SERVER --rule-name AllowAzure `
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 --only-show-errors | Out-Null
az postgres flexible-server db create `
  --resource-group $RG --server-name $PG_SERVER --database-name $PG_DB --only-show-errors | Out-Null

Write-Host "==> Azure Cache for Redis $REDIS_NAME (C0 Basic)" -ForegroundColor Cyan
az redis create `
  --resource-group $RG --name $REDIS_NAME --location $LOCATION `
  --sku Basic --vm-size c0 --minimum-tls-version 1.2 --only-show-errors | Out-Null
$REDIS_KEY_OUT = az redis list-keys --resource-group $RG --name $REDIS_NAME --query primaryKey -o tsv

Write-Host "==> Service Bus namespace $SB_NAMESPACE (Standard)" -ForegroundColor Cyan
az servicebus namespace create `
  --resource-group $RG --name $SB_NAMESPACE --location $LOCATION `
  --sku Standard --only-show-errors | Out-Null
$SB_CONN_OUT = az servicebus namespace authorization-rule keys list `
  --resource-group $RG --namespace-name $SB_NAMESPACE `
  --name RootManageSharedAccessKey --query primaryConnectionString -o tsv

Write-Host ""
Write-Host "==================== PROVISION COMPLETE ====================" -ForegroundColor Green
Write-Host "Paste these into config.ps1, then run 03-migrate.ps1 and 04-deploy.ps1:" -ForegroundColor Yellow
Write-Host ""
Write-Host "`$REDIS_KEY = `"$REDIS_KEY_OUT`""
Write-Host "`$SB_CONN   = `"$SB_CONN_OUT`""
Write-Host ""
Write-Host "(Redis conn + Service Bus conn are derived from these in config.ps1.)"
