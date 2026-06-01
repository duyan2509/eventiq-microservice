# Infrastructure — EventIQ Azure Deployment & Load Test Context

## 1. Architecture Overview

```
                        Internet
                           │
                    ┌──────▼──────┐
                    │  NGINX      │  AKS Ingress
                    │  Ingress    │  (eventiq.< domain >)
                    └──────┬──────┘
                           │ /gateway/*
                    ┌──────▼──────┐
                    │ ApiGateway  │  :8080  (Ocelot)
                    └──┬──┬──┬───┘
          ┌────────────┘  │  └───────────────┐
    ┌─────▼─────┐  ┌──────▼──────┐  ┌────────▼──────┐
    │UserService│  │  OrgService │  │ EventService  │
    │  :8081    │  │   :8082     │  │    :8083      │
    └─────┬─────┘  └──────┬──────┘  └────────┬──────┘
          │               │                   │
          └───────────────┼───────────────────┘
                          │
                  ┌───────▼──────┐
                  │  SeatService │  :8084  (SignalR + Redlock)
                  └──────┬───────┘
                         │
         ┌───────────────┼────────────────────┐
   ┌─────▼──────┐  ┌─────▼──────┐  ┌──────────▼─────────┐
   │ Azure DB   │  │ Azure Redis │  │   Azure Blob       │
   │ PostgreSQL │  │   Cache     │  │   Storage          │
   └────────────┘  └────────────┘  └────────────────────┘
                                            ▲
                          ┌─────────────────┘
                   ┌──────┴──────┐
                   │EmailService │  :8085  (RabbitMQ consumer)
                   └─────────────┘
                          │
                   ┌──────▼──────┐
                   │  CloudAMQP  │  RabbitMQ (keep existing)
                   └─────────────┘
```

---

## 2. Azure Resources

| Resource | SKU / Tier | Purpose |
|---|---|---|
| Azure Kubernetes Service | B2s × 2 nodes (System pool) | Container orchestration |
| Azure Container Registry | Basic | Docker image storage |
| Azure Database for PostgreSQL Flexible Server | B1ms (1 vCore, 2 GB) | All service schemas on one server |
| Azure Cache for Redis | C1 Basic (1 GB) | Seat presence, selections, Redlock, output cache |
| Azure Blob Storage | Standard LRS | Event banners, user avatars |
| Azure Key Vault | Standard | Connection strings, JWT keys |
| Azure Public IP + DNS label | — | Ingress endpoint |

> Existing: Blob Storage (`eventiqstr`) already provisioned with `event-banners` and `user-avatars` containers.
> CloudAMQP RabbitMQ remains in use for email events (no change needed).

---

## 3. Azure CLI — Provision Resources

```bash
# Variables
RG=eventiq-rg
LOCATION=southeastasia
ACR=eventiqacr
AKS=eventiq-aks
REDIS=eventiq-redis
PG=eventiq-pg
KV=eventiq-kv

# Resource group
az group create --name $RG --location $LOCATION

# Container Registry
az acr create --resource-group $RG --name $ACR --sku Basic
az acr login --name $ACR

# AKS (attach ACR so nodes can pull images without extra auth)
az aks create \
  --resource-group $RG \
  --name $AKS \
  --node-count 2 \
  --node-vm-size Standard_B2s \
  --attach-acr $ACR \
  --enable-oidc-issuer \
  --enable-workload-identity \
  --generate-ssh-keys

az aks get-credentials --resource-group $RG --name $AKS

# PostgreSQL Flexible Server
az postgres flexible-server create \
  --resource-group $RG \
  --name $PG \
  --admin-user eventiq \
  --admin-password "<strong-password>" \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --public-access 0.0.0.0

# Azure Cache for Redis
az redis create \
  --resource-group $RG \
  --name $REDIS \
  --sku Basic \
  --vm-size c1 \
  --location $LOCATION

# Key Vault
az keyvault create --resource-group $RG --name $KV --location $LOCATION
```

---

## 4. Secrets in Key Vault

Store the following secrets before deploying:

```bash
KV=eventiq-kv

az keyvault secret set --vault-name $KV --name postgres-connection-string \
  --value "Host=<pg>.postgres.database.azure.com;Database=neondb;Username=eventiq;Password=<pwd>;SSL Mode=Require"

az keyvault secret set --vault-name $KV --name redis-connection-string \
  --value "<redis-name>.redis.cache.windows.net:6380,password=<key>,ssl=True,abortConnect=False"

az keyvault secret set --vault-name $KV --name rabbitmq-connection-string \
  --value "amqps://zbiaqafr:yegz8imBRoFKi47FjaY7JW4g2UZ0bMaX@gerbil.rmq.cloudamqp.com/zbiaqafr"

az keyvault secret set --vault-name $KV --name azure-blob-connection-string \
  --value "DefaultEndpointsProtocol=https;AccountName=eventiqstr;AccountKey=<key>;EndpointSuffix=core.windows.net"

# RSA JWT keys (base64-encoded file contents)
az keyvault secret set --vault-name $KV --name jwt-private-key \
  --file ./keys/private.key

az keyvault secret set --vault-name $KV --name jwt-public-key \
  --file ./keys/public.key
```

---

## 5. Build & Push Docker Images

Each service has a `Dockerfile` in its project folder. Build and push to ACR:

```powershell
$ACR = "eventiqacr.azurecr.io"

$services = @(
  @{ name = "api-gateway";   dir = "Eventiq.ApiGateway"         },
  @{ name = "user-service";  dir = "Eventiq.UserService"        },
  @{ name = "org-service";   dir = "Eventiq.OrganizationService"},
  @{ name = "event-service"; dir = "Eventiq.EventService"       },
  @{ name = "seat-service";  dir = "Eventiq.SeatService"        },
  @{ name = "email-service"; dir = "Eventiq.EmailService"       }
)

foreach ($svc in $services) {
  docker build -t "$ACR/$($svc.name):latest" -f "$($svc.dir)/Dockerfile" .
  docker push "$ACR/$($svc.name):latest"
}
```

---

## 6. Kubernetes Manifests (`k8s/`)

Directory layout to create:

```
k8s/
  namespace.yaml          # namespace: eventiq
  configmap.yaml          # non-secret env vars (ASPNETCORE_ENVIRONMENT, etc.)
  secrets.yaml            # secretProviderClass — pulls from Key Vault via CSI driver
  gateway/
    deployment.yaml       # ApiGateway, 2 replicas, port 8080
    service.yaml          # ClusterIP :8080
  user-service/
    deployment.yaml       # port 8081
    service.yaml
  org-service/
    deployment.yaml       # port 8082
    service.yaml
  event-service/
    deployment.yaml       # port 8083
    service.yaml
  seat-service/
    deployment.yaml       # port 8084, mounts RSA keys from secret
    service.yaml
  email-service/
    deployment.yaml       # port 8085
    service.yaml
  ingress.yaml            # NGINX ingress — routes /gateway/* to api-gateway:8080
                          # also routes /hubs/* to seat-service:8084 (WebSocket)
```

### Key deployment annotations

**SeatService** needs two annotations in the ingress for SignalR WebSocket support:

```yaml
# In ingress.yaml for /hubs/* path:
nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
nginx.ingress.kubernetes.io/websocket-services: "seat-service"
nginx.ingress.kubernetes.io/upstream-hash-by: "$http_x_forwarded_for"  # sticky routing for SignalR
```

**Ocelot gateway** (`ocelot.json`) service URLs change from `localhost` to Kubernetes service DNS:

```
UserService      → http://user-service:8081
OrgService       → http://org-service:8082
EventService     → http://event-service:8083
SeatService      → http://seat-service:8084
```

### RSA key volume mount (all services that verify JWT)

```yaml
volumes:
  - name: jwt-keys
    csi:
      driver: secrets-store.csi.k8s.io
      readOnly: true
      volumeAttributes:
        secretProviderClass: eventiq-keyvault
volumeMounts:
  - name: jwt-keys
    mountPath: /app/keys
    readOnly: true
```

---

## 7. Verify Deployment

```bash
kubectl get pods -n eventiq          # all Running
kubectl get ingress -n eventiq       # get external IP / hostname
kubectl logs -n eventiq deploy/seat-service --tail=50
```

Smoke test:
```bash
INGRESS=<external-ip-or-hostname>

# Health
curl http://$INGRESS/gateway/health

# Login
curl -X POST http://$INGRESS/gateway/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"eventiq@gmail.com","password":"Admin@123"}'
```

---

## 8. Load Test Setup

The seat model is **flat** (seats carry an absolute `{x,y}` position; no
sections/rows). Booking reads are split into metadata + viewport seat chunks:
`GET /seat-maps/sessions/{id}/meta` (cached) and
`GET /seat-maps/sessions/{id}/seats?x1&y1&x2&y2` (omit bbox = all seats).

### Option A — Deterministic SQL seed (recommended)

`data/seed_load_test_data.sql` seeds everything needed **without** driving
the async approval/clone pipeline, so the IDs are fixed and stable
(see `data/README.md` for the full data description):

```bash
# STEP 0 first: start services once (seeds roles), register loadtest@eventiq.dev,
#               then get its UUID: SELECT id FROM user_service.users WHERE email='loadtest@eventiq.dev';

psql "<connection-string>" \
  -v load_test_user_id="'<uuid-of-loadtest-user>'" \
  -f data/seed_load_test_data.sql
# optional larger map for the viewport test:  -v seat_count=5000
```

It seeds:
- 1 org `a0000000-…-0001`, 1 event `b0000000-…-0001` (`Approved`), org payment info
- 602 charts + 601 sessions (600 for the 02 create/list load + 1 fixed published session)
- A **Published template** seat map + **Published session clone** with `seat_count` flat
  seats (grid) — gives a session with a real, sizeable seat map
- An empty **Draft** seat map owned by the org — for the SignalR designer test

Fixed IDs printed by the script (used as k6 env vars):

| Env var | Value | Used by |
|---|---|---|
| `SESSION_ID`  | `e0000000-0000-0000-0000-0000000000aa` | 01-layout-cache, 04-viewport-compare |
| `SEAT_MAP_ID` | `d0000000-0000-0000-0000-0000000000cc` (Draft) | 03-signalr-design |
| `EVENT_ID`    | `b0000000-0000-0000-0000-000000000001` | 02-seat-api |
| `ORG_ID`      | `a0000000-0000-0000-0000-000000000001` | 02, 03 |

> The k6 scripts log in themselves via `ORG_EMAIL`/`ORG_PASSWORD` (config.js),
> so no `TOKEN` env var is needed.

### Option B — Exercise the real approval → clone pipeline (optional)

To validate the live MassTransit clone path instead of seeding clones directly:
seed only the base data (skip the seat_service section), keep the event in
`Submitted` (not `Approved`), create + publish a template seat map via the API,
then have an admin accept the submission:

```bash
INGRESS=https://<aks-ingress>
# org login + role switch (auth/login, auth/role) → ORG_TOKEN  (see config.js creds)
# create template:  POST $INGRESS/gateway/seat-maps {eventId, chartId, name}
# add seats:        SignalR AddSeat on /hubs/seat-design  (flat seats)
# publish:          POST $INGRESS/gateway/seat-maps/{id}/publish
# admin approve (triggers EventApproved → SessionSeatMapCloneRequested per session):
ADMIN_TOKEN=$(curl -s -X POST $INGRESS/gateway/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"eventiq@gmail.com","password":"Admin@123"}' | jq -r .accessToken)
curl -s -X POST $INGRESS/gateway/events/b0000000-0000-0000-0000-000000000001/submissions/accept \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"message":"Approved for load test"}'
# wait ~10s for clones, then read a session id from GET /gateway/events/{eventId}
```

### Run k6 against Azure

```powershell
# From the backend repo root
$INGRESS    = "https://<aks-ingress>"
$SEAT_SVC   = "https://<aks-ingress>"   # routed via ingress /hubs/*
$WS_BASE    = "wss://<aks-ingress>"
$SESSION_ID = "e0000000-0000-0000-0000-0000000000aa"
$SEAT_MAP_ID= "d0000000-0000-0000-0000-0000000000cc"

# 01 — Layout meta cache (100 VUs, served from Azure Redis output cache)
k6 run `
  -e BASE_URL=$INGRESS/gateway -e SESSION_ID=$SESSION_ID `
  k6/seat-design-test/01-layout-cache.js

# 02 — Seat API CRUD (50 VUs)
k6 run -e BASE_URL=$INGRESS/gateway k6/seat-design-test/02-seat-api.js

# 03 — SignalR concurrent designers (20 VUs)
k6 run `
  -e BASE_URL=$INGRESS/gateway -e SEAT_SVC_URL=$SEAT_SVC -e SEAT_SVC_WS=$WS_BASE `
  -e SEAT_MAP_ID=$SEAT_MAP_ID `
  k6/seat-design-test/03-signalr-design.js

# 04 — Viewport vs get-all seat loading
k6 run `
  -e BASE_URL=$INGRESS/gateway -e SESSION_ID=$SESSION_ID `
  k6/seat-design-test/04-viewport-compare.js
```

Or run all four in sequence: `k6/seat-design-test/run.ps1 -SessionId <id> -SeatMapId <id>`
(the scripts authenticate themselves via the `ORG_EMAIL`/`ORG_PASSWORD` in config.js).

---

## 9. Monitor Redis During Load Test

### Azure Portal — Redis Console

Navigate to: **Azure Cache for Redis → Console**

```bash
# Watch presence keys grow as SignalR VUs join
KEYS seat:presence:*

# Inspect how many users are in the map
HLEN seat:presence:<seatMapId>

# Check output cache entries
KEYS *SeatMapLayout*

# Watch Redlock keys (short-lived, 30s TTL)
KEYS seat-lock:*
```

### Expected Redis behaviour during tests

| Test | Key pattern | Expected |
|---|---|---|
| `01-layout-cache` | `*SeatMapLayout*` | meta key set on first miss, all 100 VUs served from cache hit |
| `04-viewport-compare` | `*SeatMapSeats*` | one cache entry per distinct bbox query string |
| `03-signalr-design` | `seat:presence:<id>` | Hash grows to ~20 entries as VUs join, shrinks to 0 when all leave |

---

## 10. Environment Variable Reference

| Variable | Used in | Value |
|---|---|---|
| `BASE_URL` | all tests | `https://<aks-ingress>/gateway` (or `http://localhost:5001/gateway`) |
| `SEAT_SVC_URL` | 03 | `https://<aks-ingress>` (or `http://localhost:5234`) |
| `SEAT_SVC_WS` | 03 | `wss://<aks-ingress>` (or `ws://localhost:5234`) |
| `SESSION_ID` | 01, 04 | `e0000000-0000-0000-0000-0000000000aa` (seeded) |
| `SEAT_MAP_ID` | 03 | `d0000000-0000-0000-0000-0000000000cc` (seeded Draft) |
| `ORG_ID` | 02, 03 | `a0000000-0000-0000-0000-000000000001` |
| `EVENT_ID` | 02 | `b0000000-0000-0000-0000-000000000001` |
| `ORG_EMAIL` / `ORG_PASSWORD` | all tests | load-test org user creds (scripts self-login) |
