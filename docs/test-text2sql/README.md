# Test — Text2SQL analytics accuracy

Evaluates the AnalyticsService Text2SQL pipeline (natural language → SQL → chart).

## Run
Eval harness in `Eventiq.AnalyticsService/`:
```powershell
cd Eventiq.AnalyticsService
.\.venv\Scripts\python scripts/run_test_questions.py   # uses scripts/eval_questions.json
```
Run **3×** (the LLM is non-deterministic) and report variance. Bucket the
questions easy / medium / hard and report per-bucket accuracy.

## Metrics & targets
| Metric | Target |
|---|---|
| Crash rate | 0 % |
| SQL execute success rate | ≥ 90 % |
| Expected-tables hit rate | ≥ 70 % |
| Graph schema-linking usage (vs keyword fallback) | ≥ 60 % |
| PascalCase quoting violations | 0 |
| Self-correction retry rate | record |
| End-to-end latency (Groq) p50 / p95 | record |
| Tokens in/out + est. cost per question | record |

## Local vs Azure
| Metric | Local | Azure AKS |
|---|---|---|
| SQL execute success rate | | |
| Expected-tables hit rate | | |
| p95 latency | | |

> Expanding `eval_questions.json` from 20 → 50–100 questions (per `remaining_work.md`)
> gives more statistically meaningful accuracy numbers.
