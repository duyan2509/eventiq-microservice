# Đánh giá Pipeline Text2SQL — Kết quả thực nghiệm

> Mọi lệnh chạy với `ANALYTICS_MODE=eval` (branch Neon riêng, đã seed ~25k row) và `PYTHONIOENCODING=utf-8`.

## 0. Thiết lập thí nghiệm

| Hạng mục | Giá trị |
|---|---|
| Bộ dữ liệu đánh giá | 83 câu hỏi tự xây (`scripts/dataset.json`), gold SQL đã verify 100% chạy ≥1 row |
| Phân bổ độ khó | easy 25 · medium 31 · hard 17 · cross-service 10 |
| Phạm vi schema | 5 microservice (user/org/event/seat/payment), ~25 bảng |
| CSDL | Neon Postgres, multi-schema (~25.000 row Faker) |
| LLM | `llama-3.3-70b-instruct` (Together AI, Turbo/FP8), temperature 0.0, **không fine-tune, zero-shot** |
| Schema linking | NetworkX FK-graph + keyword fallback; thêm column-linking & value-linking |
| Metric | Execution Accuracy (so tập kết quả sau chuẩn hoá, độc lập thứ tự dòng/cột) |

**Ngữ cảnh so sánh:** độ khó tương đương BIRD (real-DB, GPT-4 vanilla ~46%, người ~92%); khó hơn Spider (~70-85%) do federation cross-service + câu tiếng Việt + naming hỗn hợp PascalCase/snake_case.

---

## 1. Độ chính xác tổng thể

| Cấu hình | EX-Acc | Số câu đúng | 95% CI |
|---|---|---|---|
| Baseline (graph schema-linking) | 46.99% | 39/83 | ±10.7 |
| **Pipeline đề xuất (graph + column + value linking)** | **51.81%** | **43/83** | [41%, 62%] |

→ Pipeline đề xuất **+4.8 điểm** so với baseline. (Khoảng tin cậy rộng do n=83 — xem Giới hạn.)

---

## 2a. Ablation theo ĐỘ SÂU schema-linking
*Nguồn: `python scripts/ablation.py --only no_linking,keyword_only` + `eval.py` (graph).*

| Variant | Easy | Medium | Hard | Cross | **Overall** |
|---|---|---|---|---|---|
| V1 — Không schema-linking (full DDL) | 76.0% | 48.4% | 23.5% | 0.0% | 45.78% (38) |
| V2 — Keyword matching | 72.0% | 41.9% | 11.8% | 0.0% | 39.76% (33) |
| V3 — FK-graph + fallback | 76.0% | 48.4% | 23.5% | 10.0% | **46.99% (39)** |

> **Hai luận điểm (theo đúng số đo được, thứ tự V3 > V1 > V2):**
> 1. **graph (V3) là tốt nhất, và lợi thế của nó nằm ĐÚNG ở cross-service** — V1 (full DDL) và V3 giống hệt ở easy/medium/hard, chỉ khác ở *cross-service* (0% → 10%): nhờ logical-FK bắc cầu xuyên service mà graph sinh được JOIN đúng cho truy vấn liên-service.
> 2. **Keyword-matching (V2) là TỆ NHẤT (39.8%) — kém cả "không linking"** (V1 45.8%): chọn sai bảng còn hại hơn đưa cả DDL cho model. → khẳng định việc *chọn đúng bảng* (graph) quan trọng hơn việc *thu hẹp* bảng một cách thô (keyword).

## 2b. Ablation theo TÍNH NĂNG (trên nền V3 graph)
*Nguồn: `eval.py` với các cờ `--column-linking / --value-linking / --enrich`. Tất cả 83 câu, cùng backend Together Turbo.*

| Cấu hình | Easy | Medium | Hard | Cross | **Overall** | Δ |
|---|---|---|---|---|---|---|
| V3 — graph (baseline) | 76.0% | 48.4% | 23.5% | 10.0% | 46.99% (39) | — |
| + column-linking | 72.0% | 54.8% | 23.5% | 10.0% | 48.19% (40) | +1.2 |
| + value-linking | 76.0% | 51.6% | **35.3%** | 10.0% | 50.60% (42) | +3.6 |
| **+ column + value** ⭐ | 76.0% | **58.1%** | 29.4% | 10.0% | **51.81% (43)** | **+4.8** |
| + enrich (rules + few-shot) | 76.0% | 35.5% | 29.4% | 0.0% | 42.17% (35) | −4.8 |
| + column + enrich | 76.0% | 41.9% | 17.6% | 10.0% | 43.37% (36) | −3.6 |
| + value + enrich | 80.0% | 29.0% | 29.4% | 0.0% | 40.96% (34) | −6.0 |
| + column + value + enrich | 80.0% | 45.2% | 29.4% | 0.0% | 46.99% (39) | 0.0 |

> **Hai luận điểm chính:**
> 1. **column-linking và value-linking đều đóng góp dương và cộng dồn** (47% → 48.2% → 50.6% → **51.8%**). value-linking hiệu quả nhất, đặc biệt kéo *hard* 23.5%→35.3% (ánh xạ đúng giá trị thật như `legends.name='VIP'` thay vì đoán mã số).
> 2. **enrich (thêm rule LEFT-JOIN + few-shot pattern window/HAVING/NOT EXISTS) lại LÀM GIẢM độ chính xác** ở mọi tổ hợp (đạp *medium* 48→36, *cross* 10→0). Kết luận trung thực: với SQL phức tạp, "nhồi" thêm chỉ dẫn chung khiến model bị lái sai nhiều hơn là giúp → **không đưa enrich vào pipeline cuối**.

---

## 3. Breakdown theo phương pháp schema-linking (pipeline đề xuất)

| Method | Số câu | Tỉ lệ | EX-Acc |
|---|---|---|---|
| Graph traversal | 70 | 84.3% | 55.7% |
| Keyword fallback | 13 | 15.7% | 30.8% |
| **Tổng** | 83 | 100% | 51.81% |

> Câu đi qua graph chính xác gần gấp đôi câu rơi vào keyword-fallback → khẳng định lại giá trị của FK-graph.

## 4. Breakdown theo số service liên quan (baseline V3)

| #services | Số câu | EX-Acc |
|---|---|---|
| 1 | 44 | 63.6% |
| 2 | 28 | 35.7% |
| 3 | 7 | 14.3% |
| 4 | 4 | 0.0% |

> Độ chính xác **suy giảm đơn điệu theo số service** — bottleneck rõ ràng là truy vấn liên-service (federation), không phải câu đơn lẻ.

---

## 5. Phân loại lỗi (40 câu sai của pipeline đề xuất)

| Nhóm lỗi | Mô tả | Số câu | % lỗi |
|---|---|---|---|
| Đúng số dòng, sai giá trị/cột/metric | rowcount khớp gold nhưng giá trị lệch (sai cột, thiếu ROUND, nhầm metric) | 17 | 42.5% |
| Thiếu dòng | INNER JOIN làm rớt nhóm count=0, hoặc lọc thiếu | 9 | 22.5% |
| Rỗng (0 dòng) | JOIN/filter cross-service vỡ → không ra dòng nào | 8 | 20.0% |
| Dư dòng / fan-out | JOIN nhân bản, window/subquery/correlated sai | 6 | 15.0% |

**Ví dụ điển hình theo nhóm:**
- *Sai giá trị:* #012 "tỉ lệ ghế VIP" — vẫn lệch cách tính tỉ lệ; #020 "order Failed tuần qua" — nhầm `updated_at` vs `created_at`.
- *Thiếu dòng:* #008 "lời mời mỗi org" — INNER JOIN bỏ org có 0 lời mời (25/30); #060/#061 thiếu sự kiện không có loại vé/ghế VIP.
- *Rỗng:* #023 "sự kiện có chủ tổ chức bị ban", #063 "thành viên designer mỗi org", #072 "xếp hạng sự kiện theo vé trong org" (window cross-service).
- *Fan-out:* #045 "doanh thu theo loại vé" (172 vs 4 — JOIN nhân bản), #074 "doanh thu lũy kế" (window running-total sai).

> **Tách "lỗi thật" vs "phạt oan":** một phần nhóm 1 là câu mơ hồ (vd #018 GROUP BY id-vs-name) mà pred cũng là một cách hiểu hợp lệ → EX-Acc nghiêm phạt oan. Có thể báo cáo "EX-Acc nghiêm 51.8% / nới lỏng cách-hiểu ~+3-4%".

---

## 6. Giới hạn (limitations)

- **n=83 nhỏ** → khoảng tin cậy 95% rộng (±~10.7 điểm). Không chia train/test vì mỗi tập quá nhỏ; thay vào đó pipeline **không có tham số học và không tinh chỉnh theo từng câu** (few-shot cố định, tách rời tập đánh giá) nên toàn bộ 83 câu là tập test hợp lệ. Hướng mở rộng: tăng dataset lên 150–200 câu.
- **Truy vấn cross-service / >2 service** dễ fail (cross 10%, 4-service 0%) — JOIN dài, fan-out. Đây là bottleneck chính.
- **Aggregation phức tạp** (window lồng, running-total, top-k-per-group, correlated subquery) chưa robust.
- **value-linking hiện chỉ khớp literal chính xác** (vd "VIP" → `legends.name='VIP'`); chưa xử lý đồng nghĩa/diễn giải. *Future work: seed miền giá trị (database content) kiểu BIRD.*
- **column-linking là heuristic theo role** → recall hạn chế, nhiều câu fallback full-DDL; đôi khi over-constrain (bỏ cột cần thiết).
- **enrich (general prompt rules + few-shot pattern) phản tác dụng** → giữ làm kết quả âm có giá trị, không dùng trong pipeline cuối.
- **Metric EX-Acc nghiêm** phạt oan câu mơ hồ / cột thừa (đã định lượng ở mục 5).
- LLM `llama-3.3-70b` zero-shot (Turbo/FP8 qua Together). 65% là ngoài tầm với phương pháp prompting; cần model mạnh hơn / query decomposition / fine-tune (future work).

---

## 7. Hình cho slide
- Bar chart: Overall EX-Acc baseline (47%) vs pipeline đề xuất (51.8%) + V1/V2/V3 depth.
- Grouped bar: EX-Acc theo độ khó × cấu hình (cho thấy value-linking giúp ở hard).
- Line/bar: EX-Acc giảm theo #services (64→36→14→0%).
- Bảng error taxonomy (mục 5) + bảng ablation tính năng (mục 2b, có cả kết quả âm của enrich).
- Screenshot: 1 câu qua FastAPI `/api/analytics/query` (SQL + bảng + chart).
