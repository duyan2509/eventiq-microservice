# DEPLOY — EventIQ

Ordered runbook to bring EventIQ up locally and on Azure AKS.
For Azure resource provisioning details (CLI, Key Vault, k8s manifests) see
[`../infrastructure.md`](../infrastructure.md) — this file is the step-by-step driver.

## Services & ports (local)

| Service | Path | Port | Notes |
|---|---|---|---|
| ApiGateway (Ocelot) | `Eventiq.ApiGateway` | 5001 | all client traffic → `/gateway/*` |
| UserService | `Eventiq.UserService` | 5228 | auth, RSA JWT (holds private key) |
| OrganizationService | `Eventiq.OrganizationService` | 5230 | |
| EventService | `Eventiq.EventService` | 5232 | events, sessions, charts, submissions |
| SeatService | `Eventiq.SeatService` | 5234 | SignalR hubs, Redis, Redlock |
| PaymentService | `Eventiq.PaymentService` | 5236 | Stripe checkout + webhooks |
| AnalyticsService (Python) | `Eventiq.AnalyticsService` | 5238 | Text2SQL (uvicorn `src.api:app`) |

---

## 1. Local deploy

### Prerequisites
- .NET 8 SDK, Node 20+, Python 3.11+ (for AnalyticsService), `dotnet-ef` tool
- Access to the shared infra: Neon Postgres, Upstash/Redis, CloudAMQP RabbitMQ
- RSA keys present at `keys/private.key` (UserService) and `keys/public.key` (all)

### Secrets / config
- Per-service `appsettings.Development.json` holds `ConnectionStrings:Postgres`, `Redis`, `RabbitMq`, Stripe + Groq keys.
- AnalyticsService reads `Eventiq.AnalyticsService/.env` (copy from `.env.example`).
- Never commit `.env` (now gitignored).

### Database migrations (run once per schema change)
```powershell
dotnet ef database update -p Eventiq.UserService
dotnet ef database update -p Eventiq.OrganizationService
dotnet ef database update -p Eventiq.EventService
dotnet ef database update -p Eventiq.SeatService
dotnet ef database update -p Eventiq.PaymentService
```

### Run everything
```powershell
cd eventiq-microservice
.\run-all.ps1                       # starts all .NET services + AnalyticsService

# Frontend (separate terminal)
cd ..\eventiqq
npm install; npm run dev            # Vite dev server
```
AnalyticsService needs its venv first:
```powershell
cd Eventiq.AnalyticsService
python -m venv .venv; .\.venv\Scripts\pip install -r requirements.txt
```

### Smoke test
```bash
curl http://localhost:5001/gateway/auth/login -H "Content-Type: application/json" \
  -d '{"email":"eventiq@gmail.com","password":"Admin@123"}'
```

---

## 2. Azure (AKS) deploy

Resource provisioning (ACR, AKS, Postgres Flexible, Redis, Key Vault, ingress)
is in [`../infrastructure.md`](../infrastructure.md) §2–§4. Deploy order:

1. **Provision** resources (infrastructure.md §3) and store secrets in Key Vault (§4).
2. **Build & push images** to ACR (infrastructure.md §5) — include `payment-service`
   and `analytics-service` (Python image) in addition to the list there.
3. **Run migrations** against Azure Postgres (same `dotnet ef database update` commands,
   pointed at the Azure connection string).
4. **Apply manifests**: `kubectl apply -f k8s/` (namespace, configmap, secrets,
   deployments, services, ingress — infrastructure.md §6).
5. **Seed load-test / demo data** if needed: [`../data/seed_load_test_data.sql`](../data/README.md) (§8).
6. **Verify**: `kubectl get pods -n eventiq`, then smoke test via the ingress (§7).

### Gotchas
- Ocelot service URLs switch from `localhost:52xx` to k8s DNS (`user-service:8081`, …).
- SeatService ingress needs WebSocket annotations + sticky routing for SignalR (infrastructure.md §6).
- RSA keys mounted from Key Vault via CSI driver into `/app/keys`.

---

## 3. CI/CD (to build)
`.github/workflows/deploy.yml`: build all Dockerfiles → push ACR → `kubectl apply`,
using OIDC federated auth (no stored secrets), with a post-deploy smoke-test step.
