# DEMO RUN — chạy load test manual bằng CLI, xem & phân tích output

## Bước 0 — Infra phải đang chạy + biến môi trường
Nếu đã xóa `eventiq-rg`: dựng lại theo `D:\DHCNTT\kltn\1306.md` (provision → build → migrate → deploy → vá user-service Redis + seat-service port → seed). Xong lấy FQDN:

```powershell
$env:PATH = "C:\Program Files\k6;C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin;$env:PATH"
$env:AZURE_CORE_ONLY_SHOW_ERRORS = "true"
$gwHost = az containerapp show -n api-gateway  -g eventiq-rg --query "properties.configuration.ingress.fqdn" -o tsv
$ssHost = az containerapp show -n seat-service -g eventiq-rg --query "properties.configuration.ingress.fqdn" -o tsv
$gw  = "https://$gwHost/gateway"
$ss  = "https://$ssHost"
$wss = "wss://$ssHost"
$SID = "e0000000-0000-0000-0000-0000000000aa"   # SESSION_ID (clone map có 2000/5000 ghế)
$MAP = "d0000000-0000-0000-0000-0000000000cc"   # SEAT_MAP_ID (draft, cho SignalR)
cd D:\DHCNTT\kltn\eventiq-microservice
```
Login (k6 tự login bằng loadtest@eventiq.dev/LoadTest@123 trong config.js — không cần token thủ công).

---

## ĐI VÀO ĐÂU ĐỂ XEM OUTPUT — 3 nơi

### 1. Terminal (mặc định — vừa chạy vừa hiện)
- **Lúc chạy**: progress bar (VU, % iteration, thời gian).
- **Cuối test**: bảng summary k6 + block `console.log` riêng của từng script (vd "Redis Lock Concurrency Summary", "Broadcast Fanout Summary").

### 2. Web dashboard (ĐẸP NHẤT để QUAY DEMO) — biểu đồ realtime
```powershell
$env:K6_WEB_DASHBOARD = "true"
$env:K6_WEB_DASHBOARD_EXPORT = "results/report.html"
```
Rồi chạy test như thường → mở **http://127.0.0.1:5665** trong trình duyệt: biểu đồ realtime RPS / latency / VU / checks. **Quay màn hình tab này.** Hết test tự xuất `results/report.html` (mở lại offline được).
(Tắt đi khi không cần: `Remove-Item Env:\K6_WEB_DASHBOARD`)

### 3. File JSON (để phân tích / đưa vào báo cáo)
Các script có `handleSummary` ghi vào `k6/seat-design-test/results/`:
- `fanout-N10.json`, `fanout-N50.json`, `fanout-N100.json`
- (redis-lock ghi `results/redis-lock-summary.json` — nhớ tạo folder `k6/redis-lock-test/results` trước)
Muốn full time-series thô: thêm `--out json=results/raw.json` (file to, cho phân tích sâu).

---

## CHẠY TỪNG TEST (CLI)

### A. Booking — Redlock chống over-sell (THÍ NGHIỆM CHÍNH)
```powershell
k6 run -e BASE_URL=$gw -e VUS=500 -e CONTENDED=100 k6\redis-lock-test\redis-lock-test.js
```
Verify trong DB (bằng chứng không over-sell):
```powershell
$env:PGHOST="eventiq-pg.postgres.database.azure.com"; $env:PGUSER="eventiq"
$env:PGPASSWORD="<PG_PASSWORD trong config.ps1>"; $env:PGSSLMODE="require"
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -d seat_db -c "SELECT status, count(*) FROM seat_service.seats WHERE seat_map_id='d0000000-0000-0000-0000-0000000000bb' GROUP BY status;"
```

### B. Seat-design 01–04 (chạy lần lượt)
```powershell
$common = "-e","BASE_URL=$gw","-e","SEAT_SVC_URL=$ss","-e","SEAT_SVC_WS=$wss","-e","SESSION_ID=$SID","-e","SEAT_MAP_ID=$MAP"
k6 run @common k6\seat-design-test\01-layout-cache.js
k6 run @common k6\seat-design-test\02-seat-api.js
k6 run @common k6\seat-design-test\03-signalr-design.js
k6 run @common k6\seat-design-test\04-viewport-compare.js
```

### C. Broadcast fanout — sweep theo số observer (đồ thị đẹp cho collab)
```powershell
foreach($n in 10,50,100,200){ k6 run @common -e OBSERVERS=$n k6\seat-design-test\05-broadcast-fanout.js }
```

---

## PHÂN TÍCH OUTPUT — đọc số gì, nói gì khi demo

**Field chung trong bảng summary k6:**
- `http_req_duration` → `avg ... p(95)=... p(99)=...` = độ trễ request (p95 là số trình bày chính).
- `http_req_failed ... rate=X%` = tỉ lệ request lỗi (muốn ~0%).
- `checks ... X% — ✓ N ✗ M` = % assertion pass.
- `iterations`, `vus` = throughput + tải.
- Dòng `✓/✗ <tên threshold>` cuối = đậu/rớt ngưỡng. Exit 0 = pass; **exit 99 = có threshold bị vượt** (không phải crash).

**Theo từng test:**
| Test | Số chính | Pass khi | Câu chuyện demo |
|---|---|---|---|
| **Booking** | `200 OK=100, 409=400, Unexpected=0`, hold p95 | OK==N, Unexpected==0 + DB Holding==100 | "500 người tranh 100 ghế → Redlock cho đúng 100 giữ, 400 bị từ chối, **0 over-sell**" |
| **01 layout-cache** | `layout_req_duration p95` | p95 thấp (Redis cache) | "metadata layout phục vụ từ cache, p95 ~Xms ở 100 VU" |
| **02 seat-api** | `seatmap_create/read/stats p95`, error rate | error ~0, p95 < ngưỡng | "CRUD ghế chịu 50 VU, create p95 ~Xms" |
| **03 signalr** | `signalr_connect/join p95`, error | error<5% | "20 designer cùng map kết nối realtime ổn định" |
| **04 viewport** | `seats_all_duration` vs `seats_viewport_duration` | viewport << all | "viewport-loading nhanh hơn get-all bao nhiêu lần" |
| **05 fanout** | `broadcast_fanout_ms p95` theo N | p95 phẳng khi N tăng | "1 sửa → N người nhận, p95 49/59/70ms ở N=10/50/100 → scale tốt" |

**Lưu ý đọc số đúng:**
- Nếu chạy k6 TỪ LAPTOP (qua internet → Azure Singapore): mọi p95 cộng thêm ~30-60ms RTT mạng → 01/04 dễ vượt ngưỡng (ngưỡng tuned cho localhost). **Để số sạch: chạy k6 từ trong Azure cùng region** (ACI/VM southeastasia) hoặc nói rõ "đo qua WAN".
- `p(99)`/`med` chỉ hiện nếu script có `summaryTrendStats` (05 nên thêm). `p(95)` luôn có.
- Số demo đẹp hơn: bump PG `Standard_D2ds_v4` (GeneralPurpose) trước khi chạy → hết throttle burst, latency thấp & ổn định.

**Số liệu vàng nếu kịp:** thêm flag tắt Redlock rồi chạy booking ON vs OFF → OFF cho thấy **>100 success / over-sell**, ON đúng 100 → biểu đồ so sánh là đóng góp chính của luận văn.
