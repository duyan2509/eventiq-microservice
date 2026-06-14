# =============================================================================
# 04-deploy.ps1 — create/update the 8 Container Apps on the ACA environment.
#
#   external : api-gateway (all client traffic), seat-service (SignalR direct)
#   internal : user / org / event / payment / analytics  (reached by app name)
#   none     : email-service (RabbitMQ/Service Bus consumer, no HTTP ingress)
#
# Re-runnable: if an app already exists it is `update`d in place, else created.
# Secrets are stored as ACA secrets and referenced from env via secretref:.
# =============================================================================
# Continue (không Stop): az containerapp ghi stderr (vd ResourceNotFound khi check app
# tồn tại) bị PS 5.1 promote thành terminating error dù đã 2>$null. Verify state sau khi xong.
$ErrorActionPreference = "Continue"
. "$PSScriptRoot\config.ps1"

az account set --subscription $SUBSCRIPTION

$acrUser = az acr credential show -n $ACR --query username -o tsv
$acrPass = az acr credential show -n $ACR --query "passwords[0].value" -o tsv

function Deploy-App {
  param(
    [string]   $Name,
    [string]   $Image,
    [string]   $Ingress,          # external | internal | none
    [string[]] $Secrets = @(),    # "name=value"
    [string[]] $EnvVars = @(),    # "KEY=VALUE" (VALUE may be secretref:name)
    [string]   $Cpu = "0.5",
    [string]   $Memory = "1.0Gi",
    [int]      $MinReplicas = 1,
    [int]      $MaxReplicas = 2
  )

  $exists = az containerapp show -n $Name -g $RG --query name -o tsv 2>$null
  $img = "$ACR_LOGIN/${Image}:$IMAGE_TAG"

  if (-not $exists) {
    Write-Host "==> create $Name ($Ingress)" -ForegroundColor Cyan
    $cargs = @(
      "containerapp","create",
      "--name",$Name,"--resource-group",$RG,"--environment",$ACA_ENV,
      "--image",$img,
      "--registry-server",$ACR_LOGIN,"--registry-username",$acrUser,"--registry-password",$acrPass,
      "--cpu",$Cpu,"--memory",$Memory,
      "--min-replicas",$MinReplicas,"--max-replicas",$MaxReplicas
    )
    if ($Ingress -ne "none") { $cargs += @("--ingress",$Ingress,"--target-port","8080") }
    if ($Secrets.Count -gt 0) { $cargs += "--secrets"; $cargs += $Secrets }
    if ($EnvVars.Count -gt 0) { $cargs += "--env-vars"; $cargs += $EnvVars }
    az @cargs --only-show-errors | Out-Null
  }
  else {
    Write-Host "==> update $Name" -ForegroundColor Cyan
    if ($Secrets.Count -gt 0) {
      az containerapp secret set -n $Name -g $RG --secrets $Secrets --only-show-errors | Out-Null
    }
    $uargs = @("containerapp","update","--name",$Name,"--resource-group",$RG,"--image",$img)
    if ($EnvVars.Count -gt 0) { $uargs += "--set-env-vars"; $uargs += $EnvVars }
    az @uargs --only-show-errors | Out-Null
  }
}

# --- shared values -----------------------------------------------------------
$JWT_PUB  = "Jwt__PublicKeyPath=/app/keys/public.key"
$ASPNET   = "ASPNETCORE_ENVIRONMENT=Production"
$URLS     = "ASPNETCORE_URLS=http://+:8080"

# =============================== API GATEWAY =================================
Deploy-App -Name "api-gateway" -Image "api-gateway" -Ingress "external" `
  -EnvVars @(
    $ASPNET, $URLS, $JWT_PUB,
    "Cors__VercelUrl=$FRONTEND_URL",
    "Cors__VercelPreviewPattern=$VERCEL_PREVIEW_REGEX"
  )

# =============================== USER SERVICE ===============================
Deploy-App -Name "user-service" -Image "user-service" -Ingress "internal" `
  -Secrets @("pg-conn=$PG_CONN_USER","sb-conn=$SB_CONN","blob-conn=$BLOB_CONN","redis-conn=$REDIS_CONN","seed-admin-pwd=$SEED_ADMIN_PASSWORD") `
  -EnvVars @(
    $ASPNET, $URLS, $JWT_PUB, "Jwt__PrivateKeyPath=/app/keys/private.key",
    "ConnectionStrings__Postgres=secretref:pg-conn",
    # BanCheckMiddleware đăng ký IBanBlacklistService chỉ khi có key phẳng "Redis"
    # (DependencyInjection.cs:27). Thiếu → user-service 500 mọi request.
    "Redis=secretref:redis-conn",
    "AzureServiceBus__ConnectionString=secretref:sb-conn",
    "AzureBlob__ConnectionString=secretref:blob-conn",
    "Frontend__BaseUrl=$FRONTEND_URL",
    "SeedAdmin__Email=$SEED_ADMIN_EMAIL",
    "SeedAdmin__Password=secretref:seed-admin-pwd"
  )

# =============================== ORG SERVICE ================================
Deploy-App -Name "org-service" -Image "org-service" -Ingress "internal" `
  -Secrets @("pg-conn=$PG_CONN_ORG","sb-conn=$SB_CONN","blob-conn=$BLOB_CONN") `
  -EnvVars @(
    $ASPNET, $URLS, $JWT_PUB,
    "ConnectionStrings__Postgres=secretref:pg-conn",
    "AzureServiceBus__ConnectionString=secretref:sb-conn",
    "AzureBlob__ConnectionString=secretref:blob-conn",
    "Frontend__BaseUrl=$FRONTEND_URL"
  )

# ============================== EVENT SERVICE ==============================
Deploy-App -Name "event-service" -Image "event-service" -Ingress "internal" `
  -Secrets @("pg-conn=$PG_CONN_EVENT","sb-conn=$SB_CONN","blob-conn=$BLOB_CONN") `
  -EnvVars @(
    $ASPNET, $URLS, $JWT_PUB,
    "ConnectionStrings__Postgres=secretref:pg-conn",
    "AzureServiceBus__ConnectionString=secretref:sb-conn",
    "AzureBlob__ConnectionString=secretref:blob-conn",
    "Frontend__BaseUrl=$FRONTEND_URL"
  )

# =============================== SEAT SERVICE ===============================
# External ingress so SignalR clients connect directly (VITE_SEAT_HUB_BASE_URL).
Deploy-App -Name "seat-service" -Image "seat-service" -Ingress "external" -MaxReplicas 3 `
  -Secrets @("pg-conn=$PG_CONN_SEAT","sb-conn=$SB_CONN","redis-conn=$REDIS_CONN") `
  -EnvVars @(
    $ASPNET, $URLS, $JWT_PUB,
    "ConnectionStrings__Postgres=secretref:pg-conn",
    "ConnectionStrings__Redis=secretref:redis-conn",
    "AzureServiceBus__ConnectionString=secretref:sb-conn"
  )
# Sticky sessions keep a SignalR connection pinned to one replica.
az containerapp ingress sticky-sessions set -n "seat-service" -g $RG --affinity sticky --only-show-errors | Out-Null

# ============================= PAYMENT SERVICE =============================
Deploy-App -Name "payment-service" -Image "payment-service" -Ingress "internal" `
  -Secrets @("pg-conn=$PG_CONN_PAYMENT","sb-conn=$SB_CONN","stripe-secret=$STRIPE_SECRET_KEY","stripe-webhook=$STRIPE_WEBHOOK_SECRET","ticket-secret=$TICKET_SIGNING_SECRET") `
  -EnvVars @(
    $ASPNET, $URLS, $JWT_PUB,
    "ConnectionStrings__Postgres=secretref:pg-conn",
    "AzureServiceBus__ConnectionString=secretref:sb-conn",
    "Stripe__SecretKey=secretref:stripe-secret",
    "Stripe__WebhookSecret=secretref:stripe-webhook",
    "Stripe__ReturnUrl=$FRONTEND_URL/payment/success",
    "Stripe__RefreshUrl=$FRONTEND_URL/payment/cancel",
    "InternalServices__EventServiceBaseUrl=http://event-service",
    "InternalServices__SeatServiceBaseUrl=http://seat-service",
    "InternalServices__OrgServiceBaseUrl=http://org-service",
    "Frontend__BaseUrl=$FRONTEND_URL",
    "Ticket__SigningSecret=secretref:ticket-secret",
    # Fast settle for the demo: reconcile every 5s with no grace window, so a paid
    # order flips to Sold within ~10s even if the Stripe webhook is missed.
    "Reconciliation__IntervalSeconds=5",
    "Reconciliation__GraceMinutes=0"
  )

# ============================== EMAIL SERVICE ==============================
# Background consumer — no ingress.
Deploy-App -Name "email-service" -Image "email-service" -Ingress "none" `
  -Secrets @("sb-conn=$SB_CONN","smtp-pwd=$SMTP_PASSWORD") `
  -EnvVars @(
    $ASPNET,
    "AzureServiceBus__ConnectionString=secretref:sb-conn",
    "Smtp__Host=$SMTP_HOST",
    "Smtp__Port=$SMTP_PORT",
    "Smtp__Username=$SMTP_USERNAME",
    "Smtp__Password=secretref:smtp-pwd",
    "Smtp__From=$SMTP_FROM"
  )

# ============================ ANALYTICS SERVICE ============================
# Prod (FDW) mode: connects to analytics_db, where 05-setup-fdw.ps1 has imported
# the 5 service schemas as foreign tables. Same Text2SQL SQL as Neon dev mode,
# but the cross-service JOINs resolve through postgres_fdw. Run 05-setup-fdw.ps1
# (after 03-migrate) before relying on this.
# analytics-service KHÔNG deploy cho load test seat-design/booking (bỏ qua)
<# SKIPPED
Deploy-App -Name "analytics-service" -Image "analytics-service" -Ingress "internal" `
  -Secrets @("pg-password=$PG_PASSWORD","groq-key=$GROQ_API_KEY") `
  -EnvVars @(
    "ANALYTICS_MODE=prod",
    "ANALYTICS_DB_HOST=$PG_HOST",
    "ANALYTICS_DB_PORT=5432",
    "ANALYTICS_DB_NAME=$PG_DB_ANALYTICS",
    "ANALYTICS_DB_USER=$PG_ADMIN",
    "ANALYTICS_DB_PASSWORD=secretref:pg-password",
    "ANALYTICS_DB_SSLMODE=require",
    "GROQ_API_KEY=secretref:groq-key",
    "GROQ_MODEL=$GROQ_MODEL"
  )
#>

# --- report endpoints --------------------------------------------------------
$gw = az containerapp show -n api-gateway  -g $RG --query "properties.configuration.ingress.fqdn" -o tsv
$ss = az containerapp show -n seat-service -g $RG --query "properties.configuration.ingress.fqdn" -o tsv
Write-Host ""
Write-Host "==================== DEPLOY COMPLETE ====================" -ForegroundColor Green
Write-Host "Gateway (API)   : https://$gw/gateway" -ForegroundColor Yellow
Write-Host "Seat hub (WS)   : https://$ss        (SignalR /hubs/seat-design)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Set on the frontend (Vercel) and redeploy it:"
Write-Host "  VITE_API_BASE_URL      = https://$gw/gateway"
Write-Host "  VITE_SEAT_HUB_BASE_URL = https://$ss"
Write-Host ""
Write-Host "Point the Stripe webhook at: https://$gw/gateway/stripe/webhook"
