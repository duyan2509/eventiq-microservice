# Eventiq Analytics Service

Python/FastAPI microservice that powers Text2SQL — accepts natural-language questions, returns tabular data and chart configuration.

---

## Architecture

```
POST /api/analytics/query  (or /chat)
          │
          ▼
     JWT auth + role check
          │
    ┌─────┴──────────┐
    │ Admin          │ Organization / Staff
    ▼                ▼
 run_pipeline     run_pipeline_org
    │                │
    │                └─ keyword matching → org_analytics.* views
    │                   (DB-level RLS, scoped to caller's org_id)
    │
    ▼  7-stage pipeline (src/pipeline.py):
    1. Entity extraction      (LLM #1 — Groq / Together)
    2. Confidence routing     → graph traversal | keyword fallback
    3. Schema linking         → select relevant tables via FK-graph
    3.5 Column linking        → narrow to relevant columns by role
    3.6 Value linking         → map literals to real DB cell values
    4. Prompt assembly
    5. SQL generation         (LLM #2)
    6. Execute + self-correct (LLM #3, one retry on error)
    7. Chart-type heuristic
          │
          ▼
      QueryResponse  (sql, rows, columns, chartType, chartConfig, …)
```

---

## API Endpoints

### `POST /api/analytics/query`
Returns tabular data + chart config (used by the Statistics view).

**Request**
```json
{ "question": "Monthly revenue by organization this year?" }
```

**Response**
```json
{
  "question": "...",
  "title": "Monthly Revenue",
  "sql": "SELECT ...",
  "rows": [{ "month": "2024-01", "revenue": 1500000 }],
  "columns": ["month", "revenue"],
  "chartType": "bar",
  "chartConfig": { "type": "bar", "xKey": "month", "yKey": "revenue" },
  "relevantTables": ["payment_service.orders"],
  "method": "graph",
  "retries": 0,
  "error": null,
  "answer": null
}
```

### `POST /api/analytics/chat`
Same as `/query` but also populates `answer` — a natural-language summary of the result rows (used by the Chat view).

### `GET /health`
```json
{ "status": "ok", "graphLoaded": "yes" }
```

---

## Role Routing

| Role | Pipeline | Data scope |
|---|---|---|
| `Admin` | `run_pipeline` | All 5 schemas (user / org / event / seat / payment) |
| `Organization` / `Staff` | `run_pipeline_org` | `org_analytics.*` views, RLS-filtered to `org_id` in JWT |
| Any other | 403 | — |

---

## Modes

Set via `ANALYTICS_MODE` in `.env`:

| Mode | Database | When to use |
|---|---|---|
| `dev` | Main Neon DB (live app data) | Development — **never seed or truncate** |
| `eval` | Dedicated Neon branch (~25k Faker rows) | Running the 83-question evaluation suite |
| `prod` | `analytics_db` with postgres_fdw | Production deployment |

---

## Setup

```bash
# 1. Install dependencies
pip install -r requirements.txt

# 2. Copy and fill in env vars
cp .env.example .env

# 3. Start the server
uvicorn src.api:app --host 0.0.0.0 --port 8000 --reload
```

Required `.env` variables:
- `NEON_DB_HOST`, `NEON_DB_USER`, `NEON_DB_PASSWORD` — database connection
- `GROQ_API_KEY` — LLM provider (free tier at console.groq.com); set `LLM_BASE_URL` to point at Together AI or any OpenAI-compatible endpoint instead

---

## Scripts

| Script | Description |
|---|---|
| `scripts/eval.py` | Measure Execution Accuracy over the 83-question `dataset.json` |
| `scripts/ablation.py` | Run ablation variants (no_linking / keyword / graph / +col / +val / +enrich) |
| `scripts/diff_results.py` | Diff two `eval_results*.json` files, print changed questions |
| `scripts/seed_data.py` | Seed ~25k Faker rows into the eval branch |
| `scripts/setup_org_rls.sql` | Create `org_analytics.*` views, restricted role, and RLS policies |
| `scripts/refresh_schema_dump.py` | Regenerate `schema_dump.py` when the DB schema changes |
| `scripts/run_test_questions.py` | Quick smoke-test with a handful of sample questions |

```bash
# Full eval run (requires ANALYTICS_MODE=eval)
ANALYTICS_MODE=eval python scripts/eval.py --column-linking --value-linking

# Diff two result files
python scripts/diff_results.py eval_results.json eval_results_full_col_val.json
```

---

## Evaluation Artefacts

Result files produced by `scripts/eval.py` and `scripts/ablation.py`. All live at the repo root of `Eventiq.AnalyticsService/`.

| File | Produced by | Contents |
|---|---|---|
| `eval_results.json` | `eval.py` (baseline) | Per-question output for the graph schema-linking baseline (46.99%) |
| `eval_results_full_col.json` | `eval.py --column-linking` | + column linking |
| `eval_results_full_val.json` | `eval.py --value-linking` | + value linking |
| `eval_results_full_col_val.json` | `eval.py --column-linking --value-linking` | **Proposed pipeline** (51.81%) ⭐ |
| `eval_results_full_enr.json` | `eval.py --enrich` | + prompt enrichment (rules + few-shot) — degrades accuracy |
| `eval_results_full_col_enr.json` | `eval.py --column-linking --enrich` | column + enrich |
| `eval_results_full_val_enr.json` | `eval.py --value-linking --enrich` | value + enrich |
| `eval_results_full_col_val_enr.json` | `eval.py --column-linking --value-linking --enrich` | all three |
| `ablation_results.json` | `scripts/ablation.py` | Side-by-side comparison of all 8 variants across all 83 questions |
| `diff_full_col_val.json` | `scripts/diff_results.py` | Question-level diff: baseline vs proposed pipeline (which flipped correct ↔ wrong) |

Each entry in an `eval_results_*.json` file has the shape:
```json
{
  "id": 12,
  "question": "...",
  "gold_sql": "SELECT ...",
  "predicted_sql": "SELECT ...",
  "gold_result": [[...]],
  "predicted_result": [[...]],
  "correct": false,
  "error": null,
  "schema_linking_method": "graph",
  "relevant_tables": ["event_service.events"],
  "retries": 1
}
```

---

## Evaluation Results (summary)

| Configuration | EX-Acc |
|---|---|
| Baseline (graph schema-linking only) | 46.99% |
| **Proposed pipeline (+ column + value linking)** | **51.81%** |

Full ablation tables, error taxonomy, and limitations: [`EVAL_REPORT.md`](EVAL_REPORT.md).

---

## Source Layout

```
src/
├── api.py                # FastAPI app, role-based routing
├── pipeline.py           # 7-stage orchestrator (admin mode)
├── org_scope.py          # Org-scoped pipeline + ORG_SCHEMA view definitions
├── entity_extraction.py  # LLM #1 — extract entities, normalize table names
├── schema_graph.py       # Build FK-graph from live DB (NetworkX)
├── schema_linking.py     # graph_traversal + keyword_matching
├── column_linking.py     # Narrow columns by semantic role (date, amount, …)
├── value_linking.py      # Map question literals to real DB cell values
├── prompt_builder.py     # Assemble DDL + hints into the SQL-gen prompt
├── sql_generation.py     # LLM #2 — generate SQL
├── sql_runner.py         # Execute SQL + self-correct via LLM #3
├── chart_picker.py       # Heuristic chart-type selection
├── response_builder.py   # Generate title + natural-language answer
├── auth.py               # Verify JWT, extract role + org_id
└── db.py                 # Connection pool, mode-aware
```
