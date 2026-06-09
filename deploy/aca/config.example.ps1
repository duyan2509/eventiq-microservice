# =============================================================================
# deploy/aca/config.example.ps1
#
# Copy this file to  config.ps1  and fill in the blanks. config.ps1 holds
# secrets and is gitignored. All other scripts dot-source config.ps1:
#     . "$PSScriptRoot\config.ps1"
# =============================================================================

# --- Azure subscription / location ------------------------------------------
$SUBSCRIPTION = ""                       # az account show --query id -o tsv
$LOCATION     = "southeastasia"
$RG           = "eventiq-rg"

# --- Container registry + Container Apps environment -------------------------
$ACR          = "eventiqacr"             # ACR name (must be globally unique, lowercase)
$ACR_LOGIN    = "$ACR.azurecr.io"
$ACA_ENV      = "eventiq-env"            # Container Apps managed environment
$LOG_WS       = "eventiq-logs"           # Log Analytics workspace
$IMAGE_TAG    = "latest"

# --- Azure Database for PostgreSQL (Flexible Server) -------------------------
# Database-per-service: ONE server, FIVE databases (one per data-owning service).
# Each service keeps its existing EF schema name, now inside its own database.
$PG_SERVER    = "eventiq-pg"             # -> eventiq-pg.postgres.database.azure.com
$PG_ADMIN     = "eventiq"
$PG_PASSWORD  = ""                       # strong password (also becomes an ACA secret)
$PG_HOST      = "$PG_SERVER.postgres.database.azure.com"
$PG_SKU       = "Standard_B2ms"          # bump to GeneralPurpose (e.g. Standard_D2ds_v4) for heavy 500-VU runs
$PG_TIER      = "Burstable"              # set "GeneralPurpose" if you raise the SKU

# one database per service (database-per-service pattern)
$PG_DB_USER    = "user_db"
$PG_DB_ORG     = "org_db"
$PG_DB_EVENT   = "event_db"
$PG_DB_SEAT    = "seat_db"
$PG_DB_PAYMENT = "payment_db"
$PG_DATABASES  = @($PG_DB_USER, $PG_DB_ORG, $PG_DB_EVENT, $PG_DB_SEAT, $PG_DB_PAYMENT)

# Build a connection string for a database + its EF schema. Search Path is set so
# raw Dapper reads (unqualified table names) resolve to the service's schema.
function New-PgConn([string]$db, [string]$schema) {
  "Host=$PG_HOST;Database=$db;Username=$PG_ADMIN;Password=$PG_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;Search Path=$schema"
}
$PG_CONN_USER    = New-PgConn $PG_DB_USER    "user_service"
$PG_CONN_ORG     = New-PgConn $PG_DB_ORG     "org_service"
$PG_CONN_EVENT   = New-PgConn $PG_DB_EVENT   "event_service"
$PG_CONN_SEAT    = New-PgConn $PG_DB_SEAT    "seat_service"
$PG_CONN_PAYMENT = New-PgConn $PG_DB_PAYMENT "payment_service"

# Central federation database: postgres_fdw imports the 5 service schemas here so
# AnalyticsService (ANALYTICS_MODE=prod) can run cross-service Text2SQL JOINs.
# All foreign servers point at this same flexible server, just different dbname,
# using the admin credential — see 05-setup-fdw.ps1.
$PG_DB_ANALYTICS = "analytics_db"

# --- Azure Cache for Redis ---------------------------------------------------
$REDIS_NAME   = "eventiq-redis"
$REDIS_KEY    = ""                       # filled by 01-provision.ps1 output, or paste primary key
$REDIS_CONN   = "$REDIS_NAME.redis.cache.windows.net:6380,password=$REDIS_KEY,ssl=True,abortConnect=False"

# --- Azure Service Bus (Standard tier — MassTransit needs topics) ------------
$SB_NAMESPACE = "eventiq-sb"
$SB_CONN      = ""                       # filled by 01-provision.ps1 output (RootManageSharedAccessKey)

# --- Azure Blob Storage (existing) -------------------------------------------
$BLOB_CONN    = ""                       # DefaultEndpointsProtocol=https;AccountName=eventiqstr;AccountKey=...;EndpointSuffix=core.windows.net

# --- Frontend / CORS ---------------------------------------------------------
$FRONTEND_URL          = "https://eventiq.vercel.app"   # production Vercel URL
$VERCEL_PREVIEW_REGEX  = "^https://eventiq-.*\.vercel\.app$"

# --- Stripe (PaymentService) -------------------------------------------------
$STRIPE_SECRET_KEY     = ""
$STRIPE_WEBHOOK_SECRET = ""              # whsec_... (point Stripe webhook at  <gateway>/gateway/stripe/webhook)

# --- SMTP (EmailService) -----------------------------------------------------
$SMTP_HOST     = "smtp.gmail.com"
$SMTP_PORT     = "587"
$SMTP_USERNAME = ""
$SMTP_PASSWORD = ""                      # Gmail app password
$SMTP_FROM     = "Eventiq <you@gmail.com>"

# --- Seed admin (UserService) ------------------------------------------------
$SEED_ADMIN_EMAIL    = "eventiq@gmail.com"
$SEED_ADMIN_PASSWORD = "Admin@123"

# --- Groq (AnalyticsService Text2SQL) ----------------------------------------
$GROQ_API_KEY = ""
$GROQ_MODEL   = "llama-3.3-70b-versatile"

# --- Misc app secrets --------------------------------------------------------
$TICKET_SIGNING_SECRET = ""              # any long random string (ticket QR signing)

# --- Repo root (where the .sln + service folders live) -----------------------
$REPO_ROOT = (Resolve-Path "$PSScriptRoot\..\..").Path
