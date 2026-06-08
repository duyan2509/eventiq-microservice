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
# All services share ONE server + ONE database; each owns its own schema.
$PG_SERVER    = "eventiq-pg"             # -> eventiq-pg.postgres.database.azure.com
$PG_ADMIN     = "eventiq"
$PG_PASSWORD  = ""                       # strong password (also becomes an ACA secret)
$PG_DB        = "eventiq"
$PG_HOST      = "$PG_SERVER.postgres.database.azure.com"
$PG_CONN      = "Host=$PG_HOST;Database=$PG_DB;Username=$PG_ADMIN;Password=$PG_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"

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
