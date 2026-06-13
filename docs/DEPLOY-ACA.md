# DEPLOY — Azure Container Apps (no Kubernetes)

All-Azure deployment of EventIQ to **Azure Container Apps (ACA)** with
**Azure Database for PostgreSQL**, **Azure Cache for Redis**, and
**Azure Service Bus**. No k8s, no manual YAML — provisioning and deploy are four
PowerShell scripts under [`../deploy/aca/`](../deploy/aca/).

For the local-dev runbook see [`DEPLOY.md`](DEPLOY.md). The older AKS notes in
[`../infrastructure.md`](../infrastructure.md) are superseded by this file.

---

## Topology

| App | Ingress | Why |
|---|---|---|
| `api-gateway` | **external** | all client REST traffic → `https://<gw>/gateway/*` |
| `seat-service` | **external** + sticky sessions | SignalR connects **directly** (`VITE_SEAT_HUB_BASE_URL`), not via gateway |
| `user-service` | internal | reached as `http://user-service` inside the environment |
| `org-service` | internal | |
| `event-service` | internal | |
| `payment-service` | internal | Stripe webhook arrives via the gateway route `/gateway/stripe/webhook` |
| `analytics-service` | internal | Python Text2SQL in **prod (FDW) mode** — queries `analytics_db`, which federates the 5 service DBs via `postgres_fdw` (see step 3b / `05-setup-fdw.ps1`). |
| `email-service` | **none** | background Service Bus consumer, no HTTP |

Backing services (all managed, outside ACA):
- **PostgreSQL Flexible Server** — one server, **five service databases** (database-per-service:
  `user_db`, `org_db`, `event_db`, `seat_db`, `payment_db`), each holding that service's
  EF schema, plus a 6th `analytics_db` that federates all five via `postgres_fdw` for
  cross-service Text2SQL. Each service connects only to its own database.
- **Azure Cache for Redis** — SeatService presence/selections + Redlock.
- **Azure Service Bus** (Standard) — MassTransit transport in Production (the code
  switches RabbitMQ→Service Bus automatically when `ASPNETCORE_ENVIRONMENT != Development`).
- **Azure Blob Storage** (existing `eventiqstr`) — banners/avatars.

> Frontend stays on **Vercel**: after deploy, set `VITE_API_BASE_URL` and
> `VITE_SEAT_HUB_BASE_URL` to the printed gateway/seat FQDNs and redeploy it.

---

## What was prepared in the repo

- `Eventiq.AnalyticsService/Dockerfile` — Python image (`uvicorn src.api:app` on :8080).
- `Eventiq.ApiGateway/ocelot.Production.json` — same routes as `ocelot.json` but
  downstream hosts rewritten to ACA app names on port 80. `Program.cs` now loads
  `ocelot.{Environment}.json` when present, else `ocelot.json`.
- Each JWT-validating Dockerfile bakes `keys/public.key` into `/app/keys/`
  (UserService also gets `private.key`). Paths are overridden via
  `Jwt__PublicKeyPath` / `Jwt__PrivateKeyPath`.
  > ⚠️ Baking keys into images is fine for a thesis demo. For real prod, mount
  > them from Key Vault via the ACA Key Vault secret reference instead.

---

## Prerequisites

- Azure CLI (`az login` done), an Azure subscription.
- .NET 8 SDK (only for migrations) — the images build in the cloud via `az acr build`,
  so **no local Docker daemon is required**.
- Stripe keys, a Groq API key, Gmail app password, the existing Blob connection string.

---

## Steps

### 0. Configure
```powershell
cd eventiq-microservice/deploy/aca
Copy-Item config.example.ps1 config.ps1
# edit config.ps1 — subscription id, passwords, Stripe/Groq/SMTP/Blob secrets, Vercel URL
```

### 1. Provision Azure resources
```powershell
.\01-provision.ps1
```
Creates the RG, ACR, Log Analytics, ACA environment, Postgres (+`eventiq` DB +
firewall), Redis, and Service Bus. At the end it **prints `$REDIS_KEY` and
`$SB_CONN`** — paste both into `config.ps1` (the Redis/Service Bus connection
strings are derived from them).

### 2. Build & push images
```powershell
.\02-build-push.ps1
```
`az acr build` builds all 7 .NET images + the Python analytics image inside ACR.

### 3. Run database migrations
```powershell
# add your laptop IP to the Postgres firewall once, if running from your machine
.\03-migrate.ps1
```
Applies EF Core migrations for the 5 data-owning services, each into its own
database (`user_db`, `org_db`, …).

### 3b. Wire up FDW federation for analytics
```powershell
.\05-setup-fdw.ps1
```
Imports the 5 service schemas into `analytics_db` as `postgres_fdw` foreign tables
(same schema names), so AnalyticsService can run cross-service Text2SQL in prod
mode. Must run **after** migrations (the source schemas have to exist). Prints a
per-schema table count to verify the import.

### 4. Deploy the apps
```powershell
.\04-deploy.ps1
```
Creates/updates all 8 Container Apps with ingress, secrets, and env vars, and
turns on sticky sessions for `seat-service`. It prints the gateway + seat FQDNs
and the exact Vercel env vars + Stripe webhook URL to set.

### 5. Point the frontend + Stripe at it
- Vercel project env:
  - `VITE_API_BASE_URL = https://<gateway-fqdn>/gateway`
  - `VITE_SEAT_HUB_BASE_URL = https://<seat-service-fqdn>`
  then redeploy the frontend.
- Stripe Dashboard → Webhooks → endpoint `https://<gateway-fqdn>/gateway/stripe/webhook`
  (events: `checkout.session.completed`, …). Copy the signing secret into
  `config.ps1` `$STRIPE_WEBHOOK_SECRET` and re-run `04-deploy.ps1`.

---

## Verify

```powershell
az containerapp list -g eventiq-rg -o table          # all apps Running
$gw = az containerapp show -n api-gateway -g eventiq-rg --query properties.configuration.ingress.fqdn -o tsv

# login smoke test through the gateway
curl -X POST "https://$gw/gateway/auth/login" -H "Content-Type: application/json" `
  -d '{"email":"eventiq@gmail.com","password":"Admin@123"}'

# logs
az containerapp logs show -n seat-service -g eventiq-rg --tail 50
```

(Optional) seed load-test/demo data — see [`../data/README.md`](../data/README.md),
running `psql` against the Azure Postgres connection string.

---

## Cost / teardown

Burstable Postgres (B1ms) + Basic Redis (C0) + Standard Service Bus + ACA
Consumption are low-cost; scale `--min-replicas 0` on idle apps to save more.
Tear everything down with:
```powershell
az group delete --name eventiq-rg --yes --no-wait
```

---

## Gotchas

- **Service Bus must be Standard** (not Basic) — MassTransit uses topics.
- **Migrations run in Production mode** so the host registers Service Bus; the
  bus is never started during a design-time `dotnet ef` run.
- The gateway calls internal apps as `http://<app-name>` (port 80) — that DNS
  only resolves **inside** the ACA environment, which is exactly what
  `ocelot.Production.json` targets.
- `seat-service` is external **and** still reachable internally as
  `http://seat-service` for the gateway's REST proxy routes.
- HTTPS is terminated at the ACA ingress; containers listen on plain HTTP :8080.
