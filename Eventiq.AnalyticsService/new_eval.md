# Text2SQL Evaluation — Contract-Based Approach

## Vấn đề với cách cũ

- **So sánh result set**: cần ground-truth SQL, brittle khi schema đổi, không scale
- **LLM-as-judge**: subjective, tốn thêm API call, không reproducible

## Approach mới: Spec-based / Contract Evaluation

Định nghĩa **information contract** cho mỗi câu hỏi — những gì output *phải chứa*, không quan tâm SQL viết thế nào.

Verification là pure code: deterministic, reproducible, không cần LLM.

---

## Contract Format

```python
{
    "id": "q001",
    "question": "Top 5 sự kiện bán nhiều vé nhất tháng 6?",
    "category": "ranking",
    "contract": {
        "execution_success": True,
        "row_count": {"exactly": 5},
        "col_types": ["text", "numeric"],
        "sort": {"by": "numeric", "order": "desc"},
        "chart_type": ["bar", "table"],
    }
}
```

### Contract dimensions

| Field | Mô tả | Ví dụ |
|---|---|---|
| `execution_success` | SQL chạy không lỗi | `True` |
| `row_count` | Số dòng kết quả | `{"exactly": 1}` / `{"min": 1, "max": 7}` |
| `col_types` | Column types phải có | `["text", "numeric"]` / `["date", "numeric"]` |
| `sort` | Kết quả có được sort không | `{"by": "numeric", "order": "desc"}` |
| `chart_type` | Chart type nằm trong tập hợp | `["kpi"]` / `["line"]` / `["bar", "table"]` |
| `semantic_contains` | Entity từ question phải xuất hiện trong result | `{"col_type": "text", "value": "EventName"}` |
| `value_range` | Sanity check giá trị numeric | `{"min": 0}` |
| `date_range` | Kết quả nằm trong khoảng thời gian | `{"col_type": "date", "gte": "2025-06-01", "lte": "2025-06-30"}` |

---

## Question Categories & Contracts

### KPI / Single value (20%)
```python
"question": "Tổng doanh thu tháng 5 năm 2025?",
"contract": {
    "row_count": {"exactly": 1},
    "col_types": ["numeric"],
    "value_range": {"min": 0},
    "chart_type": ["kpi"],
}
```

### Trend / Time series (20%)
```python
"question": "Doanh thu theo ngày trong 7 ngày qua?",
"contract": {
    "row_count": {"min": 1, "max": 7},
    "col_types": ["date", "numeric"],
    "chart_type": ["line"],
}
```

### Ranking / Top-N (20%)
```python
"question": "Top 5 sự kiện bán nhiều vé nhất?",
"contract": {
    "row_count": {"exactly": 5},
    "col_types": ["text", "numeric"],
    "sort": {"by": "numeric", "order": "desc"},
    "chart_type": ["bar", "table"],
}
```

### Multi-join / Cross-service (20%)
```python
"question": "Tỷ lệ check-in theo từng tổ chức?",
"contract": {
    "row_count": {"min": 1},
    "col_types": ["text", "numeric"],
    "chart_type": ["bar", "table"],
}
```

### Filter + Aggregation (20%)
```python
"question": "Tổng vé đã bán của các event có trạng thái Approved?",
"contract": {
    "row_count": {"min": 1},
    "col_types": ["numeric"],
    "value_range": {"min": 0},
    "chart_type": ["kpi", "bar"],
}
```

---

## File Structure

```
eval/
├── contracts/
│   ├── generate_contracts.py   # dùng LLM generate question + contract từ schema
│   └── questions.json          # 300 cases output của generator
├── verify.py                   # ContractVerifier: check từng dimension
├── runner.py                   # chạy batch qua pipeline, collect kết quả
└── report.py                   # aggregate metrics, in bảng kết quả
```

---

## verify.py — ContractVerifier

```python
class ContractVerifier:
    def verify(self, contract: dict, pipeline_output: dict) -> VerifyResult:
        checks = {}
        checks["execution_success"] = self._check_execution(contract, pipeline_output)
        checks["row_count"]         = self._check_row_count(contract, pipeline_output)
        checks["col_types"]         = self._check_col_types(contract, pipeline_output)
        checks["sort"]              = self._check_sort(contract, pipeline_output)
        checks["chart_type"]        = self._check_chart_type(contract, pipeline_output)
        checks["semantic_contains"] = self._check_semantic(contract, pipeline_output)
        checks["value_range"]       = self._check_value_range(contract, pipeline_output)
        checks["date_range"]        = self._check_date_range(contract, pipeline_output)

        passed = [k for k, v in checks.items() if v is True]
        failed = [k for k, v in checks.items() if v is False]
        skipped = [k for k, v in checks.items() if v is None]  # dimension không có trong contract

        return VerifyResult(
            passed=len(passed),
            failed=len(failed),
            checks=checks,
            full_pass=(len(failed) == 0),
        )
```

---

## generate_contracts.py — Scale sample size

Dùng LLM để sinh question + contract từ schema dump, không cần viết tay:

```
Prompt:
  Cho schema này: [schema_dump trích gọn]
  Sinh 60 câu hỏi analytics đa dạng theo 5 category: kpi, trend, ranking, multi_join, filter_agg.
  Với mỗi câu, trả JSON gồm: id, question, category, contract (row_count, col_types, sort, chart_type, value_range nếu có).
  Không sinh expected SQL.
```

Target: **300 cases** (60 per category). Chạy 1 lần, lưu `questions.json`, dùng lại.

---

## runner.py — Batch execution

```python
async def run_eval(questions_path: str, output_path: str):
    questions = load_json(questions_path)
    verifier = ContractVerifier()
    results = []

    for q in questions:
        output = await call_pipeline(q["question"])
        verify_result = verifier.verify(q["contract"], output)
        results.append({
            "id": q["id"],
            "question": q["question"],
            "category": q["category"],
            "full_pass": verify_result.full_pass,
            "checks": verify_result.checks,
            "sql": output.get("predicted_sql"),
            "retries": output.get("retries"),
            "latency_ms": output.get("latency_ms"),
        })

    save_json(output_path, results)
```

Rate limit: chạy async với semaphore=5 để không hit Groq rate limit.

---

## report.py — Metrics

```
=== TEXT2SQL EVAL REPORT ===
Total cases:  300

Full pass rate:     78.3%  (235/300)

By contract dimension:
  execution_success:   91.7%  (275/300)
  row_count:           84.3%  (253/300)
  col_types:           88.0%  (264/300)
  sort_order:          71.7%  (143/200)   ← chỉ tính cases có sort contract
  chart_type:          87.3%  (262/300)
  semantic_contains:   65.0%  (130/200)
  value_range:         96.0%  (144/150)
  date_range:          72.0%  (108/150)

By category:
  kpi:          88.3%  (53/60)
  trend:        81.7%  (49/60)
  ranking:      75.0%  (45/60)
  multi_join:   63.3%  (38/60)   ← điểm yếu rõ nhất
  filter_agg:   83.3%  (50/60)

Self-correction:
  Triggered:    12.3%  (37/300)
  Recovered:    73.0%  (27/37)   ← 27 queries fix được nhờ retry
  Still failed: 27.0%  (10/37)

Latency (P50 / P95):  1.2s / 3.8s
```

---

## Dataset hiện tại

83 cases trong `scripts/dataset.json` — có `gold_sql`, chia theo difficulty (easy 25 / medium 31 / hard 17 / cross-service 10).
Đây là cách eval cũ: so sánh `pred_row_count == gold_row_count`. Cần giữ lại để enrich, không chạy lại LLM.

---

## Sample size target: ~300 cases

| Nguồn | Số cases | Cách lấy |
|---|---|---|
| 83 cases cũ | 83 | Reuse `eval_results_full_col_val.json`, enrich thêm `col_types` + `chart_type` bằng re-execute SQL (không cần LLM) |
| 200 cases mới | 200 | Generate question + contract bằng LLM (1 batch call), sau đó chạy pipeline (~15-20 phút, ~400 Groq calls) |
| **Tổng** | **~283** | |

### Tại sao không re-run 83 cases cũ qua LLM?
Các file `eval_results_*.json` đã có `predicted_sql`. Chỉ cần execute lại SQL trên DB để lấy `col_types` và `actual_chart_type` — không tốn quota Groq.

---

## Implementation order (26/06)

### Bước 1 — Enrich 83 cases cũ (không cần LLM)
Viết `eval/enrich_existing.py`:
- Load `eval_results_full_col_val.json`
- Execute lại `predicted_sql` trên DB
- Capture `col_types` (từ cursor.description) + `actual_chart_type` (từ `chart_picker`)
- Save `eval_enriched_83.json`

### Bước 2 — Generate 200 cases mới (1 LLM batch)
Viết `eval/generate_contracts.py`:
- 1 prompt → sinh 200 question + contract (40 per category)
- Không sinh SQL
- Save `eval/questions_new.json`

### Bước 3 — Run pipeline trên 200 cases mới (~15-20 phút)
Viết `eval/runner.py`:
- Async + semaphore=5 để không hit Groq 30 RPM
- Capture full pipeline output (predicted_sql, col_types, chart_type, retries, latency)
- Save `eval_results_new_200.json`

### Bước 4 — Verify contracts
Viết `eval/verify.py` — ContractVerifier apply lên cả 2 files.

### Bước 5 — Report
Viết `eval/report.py` — merge 83 + 200, aggregate metrics, in bảng.
