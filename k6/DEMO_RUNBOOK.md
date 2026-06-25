# Demo Runbook — k6 Load Tests (round 2026-06-14)

Hướng dẫn chạy từng test + cái cần show khi demo. Hệ thống đã deploy + seed (5000 ghế/map) trên Azure ACA.

## 0. Chuẩn bị session PowerShell (chạy 1 lần)

```powershell
# k6 + psql trên PATH
$env:PATH = "C:\Program Files\PostgreSQL\16\bin;$env:PATH"

# --- FQDN round này (ĐỔI nếu xóa/dựng lại RG) ---
$gw = "https://api-gateway.gentlemoss-407595d6.southeastasia.azurecontainerapps.io/gateway"
$ss = "seat-service.gentlemoss-407595d6.southeastasia.azurecontainerapps.io"

# k6 đọc các biến này qua __ENV (không cần -e cho từng lệnh)
$env:BASE_URL     = $gw
$env:SEAT_SVC_URL = "https://$ss"
$env:SEAT_SVC_WS  = "wss://$ss"
$env:SESSION_ID   = "e0000000-0000-0000-0000-0000000000aa"   # clone map d0..bb (Published, 5000 ghế)
$env:SEAT_MAP_ID  = "d0000000-0000-0000-0000-0000000000cc"   # draft map (SignalR 03)

# psql tới Azure PG (cho bước verify)
$env:PGHOST="eventiq-pg.postgres.database.azure.com"; $env:PGUSER="eventiq"
$env:PGPASSWORD="Ev3ntiqLoad2026Xz7Qw"; $env:PGSSLMODE="require"

cd D:\DHCNTT\kltn\eventiq-microservice
```

**Credential test** (đã seed sẵn): org user `loadtest@eventiq.dev` / `LoadTest@123`, role Organization @ org `a0000000-…-0001`.

> ⚠️ **Lưu ý latency:** k6 chạy từ laptop VN → Azure SEA có WAN RTT ~30-80ms. Test **01, 04** có ngưỡng tuned cho localhost (p95<200ms) nên có thể **exit code 99** (threshold fail) — **KHÔNG phải app lỗi**, là độ trễ mạng. Khi demo: hoặc nói rõ đây là giới hạn đo từ xa, hoặc chạy k6 trong Azure cùng region. Test **booking / 03 / 05** đo correctness/fanout nên không bị ảnh hưởng.

---

## ⭐ Test BOOKING (Redlock) — số liệu vàng của luận văn

**Chứng minh:** Redis Distributed Lock chống over-sell. 500 VU tranh 100 ghế (mỗi ghế 5 VU). Đúng → **≤100 hold thành công (200), còn lại 409, 0 lỗi**.

```powershell
k6 run -e VUS=500 -e CONTENDED=100 k6/redis-lock-test/redis-lock-test.js
```

**Show ở summary:**
```
200 OK  (held)      : 100   (expect 100)
409 Conflict        : 400   (expect 400)
Unexpected          : 0     (expect 0)
RESULT: PASS — lock prevented over-sell
```

**Verify trong DB (chứng minh không over-sell — chạy NGAY sau test, trước teardown):**
```powershell
psql -d seat_db -c "SELECT status, count(*) FROM seat_service.seats WHERE seat_map_id='d0000000-0000-0000-0000-0000000000bb' GROUP BY status;"
```
→ `Holding` phải **≤ 100** (không có chuyện 2 VU cùng giữ 1 ghế). Test tự release ở `teardown` nên chạy lại được.

---

## Test 01 — Layout cache throughput  ⚠️ latency-sensitive

**Chứng minh:** Redis output-cache trên `GET /seat-maps/sessions/{id}/meta`, 100 VU đồng thời cùng 1 cache key.

```powershell
k6 run k6/seat-design-test/01-layout-cache.js
```
**Show:** `Total requests`, `p95 latency`, `Error rate` ở summary. Điểm nhấn: error rate ~0%, throughput cao. (p95 có thể vượt 200ms do WAN → exit99, giải thích như trên.)

---

## Test 02 — Seat map REST API (CRUD)

**Chứng minh:** org user tạo→đọc→stats→xóa seat map đồng thời (ramp 20→50 VU), DB không bị rác (mỗi VU tự delete). *(Đã fix chartId `padStart(7)` để GUID hợp lệ.)*

```powershell
k6 run k6/seat-design-test/02-seat-api.js
```
**Show:** `Create p95`, `Read p95`, `Stats p95`, `Error rate` (mong đợi ~0%). Đây là test từng bị exit99 do bug GUID tối qua — giờ phải **xanh**.

---

## Test 03 — SignalR collaborative design

**Chứng minh:** nhiều designer kết nối SignalR đồng thời (ramp 10→20 VU), mỗi VU JoinSeatMap → AddSeat → UpdateSeats → cursor → DeleteSeats → Leave. Hub xử lý ghi đồng thời không race; Redis presence tăng theo VU.

```powershell
k6 run k6/seat-design-test/03-signalr-design.js
```
**Show:** `Connect p95` (<2s), `JoinSeatMap p95` (<3s), `Error rate` (<5%). Tối qua PASS.

---

## Test 04 — Viewport vs Get-all  ⚠️ latency-sensitive

**Chứng minh:** lợi ích của viewport loading — so `get_all` (toàn bộ 5000 ghế) vs `viewport` (1 góc phần tư). 2 scenario chạy nối tiếp.

```powershell
k6 run k6/seat-design-test/04-viewport-compare.js
```
**Show:** so sánh 2 dòng — viewport **nhỏ hơn nhiều cả latency lẫn payload bytes**:
```
get_all   p95 : ___ ms   avg body : ~XXXXXX bytes   (5000 ghế)
viewport  p95 : ___ ms   avg body : ~XXXX bytes     (1/4 map)
```
Điểm nhấn = **tỉ lệ giảm payload/latency** (ý nghĩa nhất ở venue lớn). p95 tuyệt đối có thể cao do WAN.

---

## Test 05 — Broadcast fanout latency

**Chứng minh:** độ trễ broadcast realtime scale theo số collaborator. 1 editor + N observer cùng map; đo editor-gửi → observer-nhận. Chạy 3 mức N.

```powershell
k6 run -e OBSERVERS=10  k6/seat-design-test/05-broadcast-fanout.js
k6 run -e OBSERVERS=50  k6/seat-design-test/05-broadcast-fanout.js
k6 run -e OBSERVERS=100 k6/seat-design-test/05-broadcast-fanout.js
```
**Show:** dòng `Fanout p50 / p95 / p99` ở mỗi N → vẽ bảng/đồ thị fanout vs N. Tối qua: N=10→49ms, N=50→59ms, N=100→70ms (gần phẳng = broadcast scale tốt). *(Đã thêm summaryTrendStats nên giờ có cả p50/p99.)*

---

## Kết quả lưu ở đâu
Mỗi test ghi JSON vào `k6/seat-design-test/results/` (hoặc `results/redis-lock-summary.json` cho booking) — dùng cho bảng số liệu §6.2.

## Teardown (khi xong hết — tiết kiệm credit)
```powershell
az group delete --name eventiq-rg --yes --no-wait
```
