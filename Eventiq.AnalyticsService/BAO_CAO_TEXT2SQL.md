# Tài liệu viết chương Text2SQL (viết lại từ đầu)

> Bộ material self-contained cho chương "Trợ lý dữ liệu thông minh (Text2SQL)" của luận văn.
> Số liệu lấy từ thực nghiệm trên `EVAL_REPORT.md`; phần này thêm **định vị kết quả**, **cấu trúc chương** và **gợi ý prose**.
> ⚠️ Mọi số benchmark ngoài (BIRD/Spider/GPT-4) là tham khảo — **phải tự tra nguồn + trích dẫn** trước khi đưa vào.

---

## A. Định vị kết quả — 51.8% có bị coi là thấp không?

**Kết luận: KHÔNG thấp cho đúng bối cảnh, nhưng con số trần cần được "khung lại" để không bị hiểu nhầm.**

### A.1 Độ khó quyết định mọi thứ
| Benchmark | Đặc điểm | EX-Acc tham khảo |
|---|---|---|
| Spider | DB sạch, 1 database, tiếng Anh | SOTA 80–90%, method đơn giản 70%+ |
| **BIRD** | DB thật "bẩn", cần value/knowledge linking, schema lớn | GPT-4 zero-shot ~46%; người ~93%; SOTA (hệ thống nặng) 70%+ |

Setup của đề tài **thuộc vùng BIRD hoặc khó hơn**, vì cộng dồn các yếu tố khó:
- **Cross-service federation:** 5 microservice, không có ràng buộc FK xuyên service → phải bắc cầu bằng *logical FK*.
- **Tiếng Việt:** ngôn ngữ low-resource với LLM (schema lại đặt tên tiếng Anh → cross-lingual mapping).
- **Naming hỗn hợp:** PascalCase (quote `"..."`) + snake_case.
- **Model mở 70B, zero-shot, không fine-tune.**

→ Trong điều kiện đó, **51.8% là kết quả hợp lý và bảo vệ được**.

### A.2 Rủi ro tri giác & cách chống
Giám khảo *không chuyên Text2SQL* dễ thấy "51.8%" là thấp. Đây là rủi ro **tri giác**, không phải khoa học. Chống bằng framing chủ động:
1. **Đặt cạnh BIRD/SOTA** (có trích dẫn) — ngang tầm GPT-4 ở điều kiện ngặt hơn.
2. **Nhấn ablation** — đóng góp là *chứng minh từng tầng grounded giúp* + *kết quả âm của enrich*, không phải "đạt SOTA".
3. **Nhấn breakdown** — easy **76%**, single-service **64%**: mạnh ở truy vấn thực tế (đúng cái demo dùng); phần tụt là hard/cross-service — bài toán ai cũng biết khó.
4. **EX-Acc nghiêm vs nới lỏng** — "strict 51.8% / nới lỏng ~55%" (trừ phạt-oan câu mơ hồ + cột thừa).

### A.3 Điểm mấu chốt khi bảo vệ
- Text2SQL là **một thành phần** của hệ thống microservices, không phải toàn bộ luận văn → bar là "chạy được + đánh giá nghiêm túc", không phải "beat SOTA".
- **Một 51.8% có ablation + error taxonomy + limitations trung thực được đánh giá cao hơn một 70% không giải thích được.** Hội đồng chấm độ chặt chẽ thực nghiệm.
- **Không cố nống số** — tinh chỉnh để khít tập test = overfitting, rủi ro nặng hơn nhiều khi bị hỏi "có tách tập test không?".

---

## B. Hệ thống & Pipeline (cho phần "Thiết kế")

Pipeline `run_pipeline` biến câu hỏi tiếng Việt → SQL → kết quả + chart. Mỗi bước là 1 module (dễ test + ablation).

| # | Bước | Module | LLM? | Vai trò |
|---|------|--------|:--:|---------|
| 1 | Entity extraction + normalize | `entity_extraction.py` | ✓ #1 | LLM chọn bảng liên quan + filter/aggregation + confidence; chuẩn hoá tên bảng (4 tầng exact→ci→suffix→fuzzy) |
| 2 | Confidence routing | `schema_linking.py` | ✗ | ngưỡng 0.7 → graph hay keyword |
| 3 | Schema linking | `schema_linking.py`, `schema_graph.py` | ✗ | chọn **bảng**: graph traversal (logical FK bắc cầu) hoặc keyword fallback |
| **3.5** | **Column-linking** | `column_linking.py` | ✗ | chọn **cột** theo *role* suy từ DDL (measure/temporal/status_enum/dimension/key); fallback full-DDL |
| **3.6** | **Value-linking** | `value_linking.py` | ✗ | `SELECT DISTINCT` lấy **giá trị thật** của cột category → khớp literal câu hỏi (vd "VIP" → `legends.name='VIP'`) |
| 4 | Prompt build | `prompt_builder.py` | ✗ | naming rules + ENUM + soft-delete + DDL subgraph + JOIN hints + **CỘT LIÊN QUAN** + **GIÁ TRỊ THỰC TẾ** + few-shot |
| 5 | SQL generation | `sql_generation.py` | ✓ #2 | sinh SQL (temp 0.0) + clean_sql |
| 6 | Execute + self-correct | `sql_runner.py` | ✓ #3 | chạy read-only; lỗi → gửi lỗi+DDL cho LLM sửa, retry ×1 |
| 7 | Chart config | `chart_picker.py` | ✗ | chọn loại chart theo hình dạng dữ liệu |

**Triết lý cốt lõi (điểm bán của thiết kế):** stage 3.5 và 3.6 **không gọi LLM, hoàn toàn grounded** — cột suy từ DDL, giá trị lấy trực tiếp từ DB. Không để LLM đoán cột/giá trị (chống hallucination). Đây là phần kéo accuracy 47% → 51.8%.

*(Sơ đồ: `docs/modules/text2sql/02-pipeline-admin.png`.)*

---

## C. Thiết lập thực nghiệm

| Hạng mục | Giá trị |
|---|---|
| Bộ dữ liệu | 83 câu tự xây, gold SQL verify 100% chạy ≥1 row |
| Phân bổ độ khó | easy 25 · medium 31 · hard 17 · cross-service 10 |
| Schema | 5 microservice (user/org/event/seat/payment), ~25 bảng |
| CSDL | Neon Postgres multi-schema, ~25.000 row (Faker) |
| LLM | `llama-3.3-70b-instruct` (Together AI, Turbo/FP8), **zero-shot, temperature 0.0** |
| Metric | Execution Accuracy — so tập kết quả sau chuẩn hoá (độc lập thứ tự dòng/cột) |

---

## D. Kết quả (số liệu chốt)

### D.1 Tổng thể
| Cấu hình | EX-Acc | Đúng | 95% CI |
|---|---|---|---|
| Baseline (chỉ graph schema-linking) | 46.99% | 39/83 | ±10.7 |
| **Pipeline đề xuất (graph + column + value)** | **51.81%** | **43/83** | [41%, 62%] |

### D.2 Ablation độ sâu schema-linking
| Variant | Easy | Med | Hard | Cross | Overall |
|---|---|---|---|---|---|
| V1 — Không linking (full DDL) | 76.0 | 48.4 | 23.5 | 0.0 | 45.78% (38) |
| V2 — Keyword matching | 72.0 | 41.9 | 11.8 | 0.0 | 39.76% (33) |
| V3 — FK-graph + fallback | 76.0 | 48.4 | 23.5 | 10.0 | 46.99% (39) |

**Luận điểm:** thứ tự **V3 > V1 > V2**. (a) Graph thắng *đúng ở cross-service* (V1↔V3 giống hệt easy/med/hard, chỉ khác cross 0%→10% nhờ logical-FK bắc cầu). (b) Keyword *tệ hơn cả không-linking* → chọn sai bảng hại hơn đưa cả DDL.

### D.3 Ablation tính năng (trên nền V3 graph)
| Cấu hình | Easy | Med | Hard | Cross | Overall | Δ |
|---|---|---|---|---|---|---|
| V3 graph | 76.0 | 48.4 | 23.5 | 10.0 | 46.99 (39) | — |
| + column | 72.0 | 54.8 | 23.5 | 10.0 | 48.19 (40) | +1.2 |
| + value | 76.0 | 51.6 | 35.3 | 10.0 | 50.60 (42) | +3.6 |
| **+ column + value** | 76.0 | 58.1 | 29.4 | 10.0 | **51.81 (43)** | **+4.8** |
| + enrich | 76.0 | 35.5 | 29.4 | 0.0 | 42.17 (35) | −4.8 |
| + column + enrich | 76.0 | 41.9 | 17.6 | 10.0 | 43.37 (36) | −3.6 |
| + value + enrich | 80.0 | 29.0 | 29.4 | 0.0 | 40.96 (34) | −6.0 |
| + column + value + enrich | 80.0 | 45.2 | 29.4 | 0.0 | 46.99 (39) | 0.0 |

**Luận điểm:** column & value linking đóng góp dương, cộng dồn (47→48→51→**51.8**); value-linking mạnh nhất (hard 24→35). **enrich (general rule + few-shot pattern) làm GIẢM ở mọi tổ hợp** → kết quả âm trung thực: nhồi chỉ dẫn chung lái model sai ở SQL phức tạp.

### D.4 Breakdown
**Theo phương pháp (pipeline đề xuất):** graph 70 câu (84.3%) — 55.7% · keyword fallback 13 câu (15.7%) — 30.8%.
**Theo số service (baseline V3):** 1→63.6% (44 câu) · 2→35.7% (28) · 3→14.3% (7) · 4→0.0% (4). → **suy giảm đơn điệu theo số service** = bottleneck federation.

### D.5 Phân loại lỗi (40 câu sai của pipeline đề xuất)
| Nhóm | Mô tả | Số | % lỗi |
|---|---|---|---|
| Đúng số dòng, sai giá trị/cột/metric | rowcount khớp nhưng giá trị lệch | 17 | 42.5% |
| Thiếu dòng | INNER JOIN bỏ nhóm count=0 / lọc thiếu | 9 | 22.5% |
| Rỗng (0 dòng) | JOIN/filter cross-service vỡ | 8 | 20.0% |
| Dư dòng / fan-out | JOIN nhân bản, window/subquery sai | 6 | 15.0% |

---

## E. Phân tích & thảo luận (ý chính để viết prose)
1. **Schema-linking grounded là yếu tố quyết định**, đặc biệt cho federation: graph (logical FK) là thứ duy nhất giải được câu cross-service mà baseline full-DDL bó tay.
2. **Value-linking đem lại gain lớn nhất** dù chỉ chạm số ít câu — vì nó sửa đúng *lớp lỗi value-mapping* mà LLM hay đoán sai (mã enum/seat_type thay vì giá trị thật).
3. **Kết quả âm của enrich là phát hiện có giá trị:** với SQL phức tạp, thêm rule/few-shot chung gây nhiễu nhiều hơn lợi — ủng hộ hướng *grounded, deterministic* thay vì *prompt-engineering chung*.
4. **Bottleneck rõ ràng = độ phức tạp + số service** (easy 76% / cross 10%, 1-svc 64% / 4-svc 0%) → định hướng future work.

---

## F. Đóng góp (claim được)
1. Pipeline Text2SQL **grounded nhiều tầng** (entity → graph table-linking qua logical-FK xuyên service → column-linking theo role → value-linking từ DB thật) cho **schema microservices đa-service, câu tiếng Việt**.
2. **Ablation chứng minh từng tầng grounded đóng góp dương & cộng dồn** (47→51.8%); value-linking hiệu quả nhất.
3. **Kết quả âm trung thực:** general prompt-enrichment làm giảm accuracy.
4. Bộ **benchmark 83 câu tiếng Việt** cho domain bán vé sự kiện (gold SQL đã verify) — tái sử dụng được.

---

## G. Giới hạn & Hướng phát triển
**Giới hạn:**
- n=83 → CI rộng (±~10.7). Không chia train/test (mỗi tập quá nhỏ); bù lại pipeline **không có tham số học, không tune theo từng câu** (few-shot cố định, tách rời tập đánh giá) → cả 83 câu là test hợp lệ.
- Cross-service / >2 service yếu (federation).
- Aggregation phức tạp (window lồng, running-total, top-k-per-group, correlated) chưa robust.
- value-linking chỉ khớp literal chính xác, chưa xử lý đồng nghĩa/diễn giải.
- EX-Acc nghiêm phạt oan câu mơ hồ / cột thừa.

**Hướng phát triển:**
- Mở rộng dataset 150–200 câu (thu hẹp CI).
- Value-linking nâng cấp: seed miền giá trị (database content) kiểu BIRD để xử lý diễn giải/đồng nghĩa.
- Query decomposition / multi-agent cho câu cross-service phức tạp.
- Model mạnh hơn hoặc fine-tune trên domain để vượt trần ~52%.

---

## H. Trích dẫn cần TỰ VERIFY (đừng dùng số của tài liệu này làm citation)
- BIRD benchmark (Li et al., NeurIPS 2023) — số GPT-4 EX-Acc, human baseline.
- Spider benchmark (Yu et al., 2018) — mức SOTA hiện tại.
- llama-3.3-70b (Meta) — thông số model.
- (Tuỳ chọn) DAIL-SQL / DIN-SQL / MAC-SQL — nếu nhắc tới method SOTA ở phần related work / future work.

---

## I. Hình/biểu đồ cần làm
1. Bar: Overall EX-Acc của V1/V2/V3/đề-xuất.
2. Grouped bar: EX-Acc theo độ khó × cấu hình (cho thấy value-linking giúp ở hard).
3. Line/bar: EX-Acc giảm theo #service (64→36→14→0%).
4. Pie: tỉ lệ graph vs keyword_fallback.
5. Bảng ablation tính năng (D.3, có cả kết quả âm enrich) + bảng error taxonomy (D.5).
6. Sơ đồ pipeline `02-pipeline-admin.png`.
7. Screenshot: 1 câu qua FastAPI `/api/analytics/query` (SQL + bảng + chart).

---

## J. Cấu trúc chương đề xuất
1. **Đặt vấn đề** — vì sao Text2SQL cho admin/org; thách thức (multi-service, tiếng Việt, naming).
2. **Thiết kế pipeline** — kiến trúc + 7 bước (mục B) + triết lý grounded; sơ đồ.
3. **Thiết lập thực nghiệm** — dataset, schema, model, metric (mục C).
4. **Kết quả** — tổng thể + 2 ablation + breakdown (mục D) + các hình.
5. **Phân tích & thảo luận** — mục E + error taxonomy.
6. **So sánh & định vị** — mục A (đặt cạnh BIRD, giải thích độ khó).
7. **Giới hạn & hướng phát triển** — mục G.

> Gợi ý mở đầu mục Kết quả (prose mẫu): *"Pipeline đề xuất đạt 51.8% Execution Accuracy trên bộ 83 câu hỏi tiếng Việt, cao hơn baseline chỉ-graph 4.8 điểm. Trong bối cảnh truy vấn cross-service trên schema microservices — độ khó tương đương benchmark BIRD — kết quả này nằm trong vùng hợp lý của các LLM mở zero-shot, đồng thời cho thấy đóng góp rõ rệt của hai tầng linking grounded mà luận văn đề xuất."*
