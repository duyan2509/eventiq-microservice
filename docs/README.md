# EventIQ docs

| Doc | What |
|---|---|
| [DEPLOY.md](./DEPLOY.md) | Deploy runbook — local + Azure AKS |
| [DEMO.md](./DEMO.md) | Defense demo script (full lifecycle, 3 contributions) |
| [test-seat-design/](./test-seat-design/README.md) | k6 seat-design / booking suite (cache, REST, SignalR, viewport) |
| [test-redis-lock/](./test-redis-lock/README.md) | Redis Distributed Lock concurrency experiment |
| [test-text2sql/](./test-text2sql/README.md) | Text2SQL analytics accuracy eval |

Detailed infra reference (Azure provisioning, k8s manifests, Redis monitoring):
[`../infrastructure.md`](../infrastructure.md).

Each `test-*` folder holds **documentation only** (metrics, targets, how to run,
local-vs-Azure tables). The k6 scripts live in
[`../k6/seat-design-test/`](../k6/seat-design-test).
