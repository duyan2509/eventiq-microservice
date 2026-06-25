# 6.2. Kiểm thử hiệu năng (load test)

Để đánh giá khả năng chịu tải của các phân hệ thời gian thực, hệ thống được kiểm thử hiệu năng trên hai phân hệ chịu tải cao nhất: **thiết kế sơ đồ cộng tác (SeatDesignHub)** và **đặt chỗ/giữ ghế (SeatBookingHub)**.

> Ghi chú nguồn số liệu: kết quả trong tài liệu này lấy trực tiếp từ các lần chạy k6 trên môi trường Azure Container Apps (14/06/2026). Các ô đánh dấu `[điền từ kết quả chạy]` là chỉ số đọc từ bản ghi demo / file `results/*.json` của lần chạy tương ứng.

---

## 6.2.1. Phương pháp và công cụ kiểm thử

### Công cụ kiểm thử

Hệ thống được kiểm thử tải bằng **Grafana k6 v2.0.0** — công cụ kiểm thử hiệu năng mã nguồn mở viết bằng Go, kịch bản viết bằng JavaScript/ES6. k6 được chọn vì:

- **Hỗ trợ WebSocket gốc** — cần thiết để mô phỏng kết nối SignalR thật (HTTP negotiate → WebSocket upgrade → SignalR JSON framing), điều mà JMeter/Locust cần plugin bổ sung.
- **Mô hình Virtual User (VU) sát thực tế** — mỗi VU giữ trạng thái riêng (JWT, connection token, seat id) và chạy đầy đủ luồng hành vi như người dùng thật, không chỉ là request/s thuần.
- **Threshold tích hợp** — tiêu chí PASS/FAIL định nghĩa ngay trong script (vd `p(95) < 2000ms`); k6 trả exit code ≠ 0 nếu vi phạm.
- **Nhẹ, một binary, không phụ thuộc JVM** — thích hợp chạy từ máy cục bộ hoặc trong cùng region cloud.

So sánh: JMeter mạnh về đa giao thức nhưng nặng tài nguyên, cú pháp XML khó bảo trì; Locust (Python) hỗ trợ WebSocket yếu hơn và overhead GIL ảnh hưởng độ chính xác khi VU lớn.

### Môi trường kiểm thử

Toàn bộ kiểm thử chạy trên **Azure Container Apps (ACA)**, region `southeastasia`, phản ánh điều kiện vận hành cloud thực tế.

**Các service (Container App):**

| Thành phần | Ingress | CPU | RAM | Replica (min/max) |
|---|---|---|---|---|
| `api-gateway` (Ocelot) | External | 0.5 vCPU | 1.0 Gi | 1 / 2 |
| `seat-service` (SignalR + Redlock) | External | 0.5 vCPU | 1.0 Gi | 1 / 3 |
| `user-service` | Internal | 0.5 vCPU | 1.0 Gi | 1 / 2 |
| `org-service`, `event-service`, `payment-service` | Internal | 0.5 vCPU | 1.0 Gi | 1 / 2 |
| `email-service` | Không (consumer) | 0.5 vCPU | 1.0 Gi | 1 / 2 |

> `seat-service` bật **sticky sessions** (session affinity) để mỗi kết nối SignalR luôn định tuyến tới đúng replica đã cấp connection token, và dùng **Redis backplane** (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) để đồng bộ broadcast giữa các replica khi scale ngang.

**Hạ tầng managed (môi trường load test):**

| Hạng mục | Cấu hình |
|---|---|
| Cơ sở dữ liệu | Azure Database for PostgreSQL Flexible Server — `Standard_B2ms` (Burstable), 5 database (database-per-service) |
| Cache / Distributed Lock | Azure Cache for Redis — Basic C0 (output cache `seat:presence`/`seat:selection` + Redlock) |
| Message broker | Azure Service Bus — Standard (thay RabbitMQ ở môi trường này) |
| Container Registry | Azure Container Registry — Basic |

> **Lưu ý về tier:** tier `B2ms` Burstable và Redis C0 được chọn để tối ưu chi phí cho thí nghiệm. Do đó các giá trị **độ trễ tuyệt đối** chịu ảnh hưởng của giới hạn burst (đặc biệt khi serialize tập ghế lớn hoặc dưới tải cao); các chỉ số **đúng/sai về mặt logic** (tỉ lệ tranh chấp xử lý đúng, fanout xuyên replica, % giảm payload) thì độc lập với tier.

**Máy phát tải (k6 client):** k6 chạy từ laptop Windows 11 (mạng tại Việt Nam) gửi tải tới endpoint HTTPS của ACA (SEA) — độ trễ đo bao gồm round-trip WAN (~30–80 ms). Với các kịch bản nhạy độ trễ và kiểm thử scale ngang (SignalR fanout), k6 còn được chạy **trong cùng region** qua Azure Container Instance để loại trừ yếu tố mạng.

### Dữ liệu nền (seed)

Trước khi chạy, dữ liệu được seed với các UUID cố định để đảm bảo tái lập:

| Thực thể | UUID cố định | Quy mô |
|---|---|---|
| Organization | `a0000000-…-0001` | — |
| Event Session | `e0000000-…-00aa` | — |
| SeatMap đặt chỗ (clone) | `d0000000-…-00bb` | **5.000 ghế** |
| SeatMap thiết kế (draft) | `d0000000-…-00cc` | — |

### Tổng quan các kịch bản

| # | Script | Phân hệ | Giao thức | Mô hình tải | VU đỉnh |
|---|---|---|---|---|---|
| 03 | `03-signalr-design.js` | **SeatDesignHub** | WebSocket | Ramping 0→10→20 | 20 |
| 05 | `05-broadcast-fanout.js` | **SeatDesignHub** (broadcast) | WebSocket | 1 editor + N observer | 100 |
| 01 | `01-layout-cache.js` | SeatService — Redis cache (REST) | HTTP | Ramping →100 | 100 |
| 04 | `04-viewport-compare.js` | SeatService — viewport loading (REST) | HTTP | 50 + 50 | 50 |
| (Redlock) | `redis-lock-test.js` | **SeatBookingHub** | HTTP | Per-VU (1 req/VU) | 500 |

---

## 6.2.2. Kiểm thử tải phân hệ thiết kế sơ đồ (SeatDesignHub)

Phân hệ thiết kế sơ đồ cho phép nhiều người dùng **cộng tác chỉnh sửa sơ đồ ghế theo thời gian thực** qua SignalR. Hai khía cạnh được đo: (a) khả năng phục vụ nhiều designer đồng thời, và (b) **độ trễ lan truyền (broadcast fanout)** — thời gian từ lúc một người thao tác tới lúc những người còn lại nhận được cập nhật.

### a) Kết nối đồng thời nhiều designer (script 03)

Kịch bản: tải tăng dần **0 → 10 → 20 VU** (~50 s), mỗi VU là một người thiết kế đồng thời, chạy đúng luồng thật: `negotiate → handshake → JoinSeatMap (nhận CurrentPresence) → AddSeat → UpdateSeats (di chuyển ghế) → SendCursorPosition ×3 → DeleteSeats + LeaveSeatMap`. Chứng minh Redis `seat:presence:{seatMapId}` tăng theo số VU và hub xử lý ghi ghế đồng thời không xung đột (mỗi ghế có label duy nhất theo VU/iteration).

**Bảng 6.2a — Kết nối đồng thời (script 03, 20 VU)**

| Chỉ số | Ngưỡng | Kết quả |
|---|---|---|
| `signalr_connect_duration` p95 | < 2 000 ms | **259 ms** ✓ |
| `signalr_join_duration` p95 | < 3 000 ms | **108 ms** ✓ |
| Tổng message nhận | > 0, không mất | **4 071** |
| `signalr_error_rate` | < 5 % | **0 %** ✓ |

### b) Độ trễ broadcast fanout và khả năng scale ngang (script 05)

Đây là phép đo cốt lõi của phân hệ cộng tác: **1 editor + N observer** cùng vào một sơ đồ. Editor phát thao tác con trỏ kèm timestamp mỗi 500 ms; mỗi observer nhận broadcast `CursorMoved` và ghi lại `fanout = thời điểm nhận − thời điểm gửi`. Vì k6 chạy trong một tiến trình nên đồng hồ chung, hai mốc thời gian so sánh trực tiếp được.

Thí nghiệm chạy ở **3 replica của `seat-service`** (cấu hình scale ngang, dùng Redis backplane + sticky session) để kiểm chứng broadcast có lan truyền đúng giữa các instance hay không.

**Bảng 6.2b — Độ trễ broadcast fanout theo số người cộng tác (3 replica)**

| N (observer) | Edit gửi | Broadcast nhận | Fanout p50 | Fanout p95 | Fanout p99 | Join OK | Error |
|---|---|---|---|---|---|---|---|
| 10 | 126 | 1 136 | 93 ms | 148 ms | 154 ms | 100 % | 0 % |
| 50 | 126 | 5 747 | 44 ms | 72 ms | 106 ms | 100 % | 0 % |
| 100 | 126 | 11 691 | 45 ms | 114 ms | 228 ms | 100 % | 0 % |

**Nhận xét:**

1. **Broadcast lan truyền đúng khi scale ngang.** Với N=100 trên 3 replica, số broadcast nhận được là **11.691 ≈ 93 %** của kỳ vọng (126 edit × 100 observer). Nếu Redis backplane không relay giữa các replica thì chỉ ~1/3 observer (cùng replica với editor) nhận được; con số ~93 % chứng minh **backplane đồng bộ broadcast xuyên cả 3 instance**, tỉ lệ join 100 % và lỗi 0 % ở mọi mức N. Đây là bằng chứng phân hệ realtime **hoạt động đúng khi scale ngang nhiều instance**.
2. **Độ trễ fanout gần như phẳng theo số người cộng tác.** Khi N tăng 10 → 100, fanout p50 ổn định 44–93 ms và p95 nằm trong dải 72–148 ms, không xấu đi theo số collaborator → cơ chế broadcast không bị nghẽn (payload con trỏ rất nhỏ). Dao động nhỏ giữa các mức N nằm trong sai số đo (percentile ở N thấp kém ổn định hơn do ít mẫu).

> **Hình 6.x:** biểu đồ fanout p50/p95/p99 theo N (10, 50, 100) — thể hiện đường latency phẳng. *(vẽ từ dữ liệu Bảng 6.2b)*

> **Lưu ý kỹ thuật (kiểm thử SignalR đa-instance):** để client k6 mô phỏng đúng hành vi trình duyệt khi `seat-service` chạy nhiều replica, kịch bản 03/05 chuyển tiếp cookie sticky-session (`acaAffinity`) nhận được ở bước `negotiate` sang yêu cầu WebSocket — đảm bảo upgrade WS rơi đúng replica đã cấp connection token (trình duyệt thật tự gửi cookie này).

### c) Bổ sung — phân hệ sơ đồ (REST)

Ba phép đo REST hỗ trợ cho phân hệ thiết kế/đặt chỗ:

- **REST CRUD sơ đồ (script 02, tải tăng tới 50 VU)** — tạo → đọc → stats → xóa sơ đồ đồng thời: create p95 **60 ms**, read p95 **60 ms**, stats p95 **63 ms**, **error 0 %** — API quản lý sơ đồ ổn định dưới tải ghi/đọc đồng thời.
- **Redis output cache (script 01, 100 VU)** trên `GET /seat-maps/sessions/{id}/meta`: **9.319 request, error 0 %**, độ trễ med 108 ms / p95 295 ms, throughput ~151 req/s — cache phục vụ ổn định dưới 100 VU đồng thời (p95 phản ánh tier Burstable; xem lưu ý §6.2.1).
- **Viewport loading (script 04, 5.000 ghế)** so sánh tải toàn bộ sơ đồ vs một viewport: payload giảm từ **1.112.300 byte (~1,1 MB) xuống 286.386 byte (~286 KB) = giảm 74,3 % (≈3,9×)**, độ trễ p95 viewport thấp hơn get-all ~6×, error 0 %. Đây là minh chứng định lượng cho lợi ích của cơ chế tải theo viewport ở sơ đồ lớn (% giảm payload độc lập với tier hạ tầng).

---

## 6.2.3. Kiểm thử tải phân hệ đặt chỗ và giữ ghế (SeatBookingHub — Redlock)

**Thí nghiệm cốt lõi của luận văn.** Kịch bản (`redis-lock-test.js`): **N = 100 ghế** bị tranh chấp bởi **500 VU đồng thời**, mỗi VU gửi đúng 1 yêu cầu `POST /seat-maps/{id}/seats/hold` cho ghế `seatIds[(VU-1) % N]` → **mỗi ghế bị 5 VU giành cùng lúc**. Yêu cầu đi qua API Gateway và có thể rơi vào **bất kỳ replica nào** của `seat-service`, nên đây cũng là phép thử cho tính đúng đắn của **distributed lock xuyên nhiều instance** (Redlock lưu khoá trên Redis dùng chung).

Kỳ vọng: cơ chế khoá phân tán phải đảm bảo đúng **100** request `200 OK`, phần còn lại `409 Conflict`, và **không có ghế Holding trùng** (không over-sell).

**Bảng 6.3 — Kết quả Redlock (500 VU tranh 100 ghế)**

| Chỉ số | Kỳ vọng | Thực tế |
|---|---|---|
| Ghế tranh chấp (N) | 100 | 100 |
| VU đồng thời | 500 | 500 |
| `200 OK` (giữ thành công) | 100 | **100** |
| `409 Conflict` (từ chối đúng) | 400 | **400** |
| Bất thường (khác 200/409) | 0 | **0** |
| `lock_hold_duration` p95 | — | 3,1 s (trung vị 1,1 s; max 6,2 s) *(tier B2ms burst; xem lưu ý §6.2.1)* |
| Số ghế `Holding` trong DB | 100 | **100, không trùng** |
| **Kết luận** | no over-sell | **PASS — lock chống over-sell** |

**Kiểm chứng ở CSDL sau khi chạy** (xác nhận không có ghế bị hai người giữ):

```sql
SELECT count(*) FROM seat_service.seats
WHERE seat_map_id = 'd0000000-0000-0000-0000-0000000000bb' AND status = 'Holding';
-- = 100 (đúng bằng N, không vượt → không over-sell)
```

**Nhận xét:** trong điều kiện 500 yêu cầu đồng thời tranh 100 ghế (mỗi ghế 5 người giành), Redlock đảm bảo **đúng 100 lượt giữ thành công, 400 lượt bị từ chối hợp lệ, 0 lỗi và 0 ghế bị giữ trùng** — kể cả khi các yêu cầu được phân tải tới nhiều replica `seat-service` khác nhau. Kết quả chứng minh cơ chế khoá phân tán Redlock loại bỏ hoàn toàn tình trạng over-sell (đặt trùng ghế) dưới tải tranh chấp cao. Độ trễ giữ ghế p95 ≈ 3,1 s (trung vị 1,1 s) phản ánh giới hạn của tier Burstable B2ms khi 500 VU cùng truy cập, không ảnh hưởng tính đúng đắn.

---

## Cách tái chạy (khi hệ thống đang triển khai)

```powershell
# Thiết lập endpoint (thay <gateway>/<seat> theo FQDN ACA hiện tại)
$gw = "https://<gateway>.<region>.azurecontainerapps.io/gateway"
$ss = "<seat>.<region>.azurecontainerapps.io"
$env:BASE_URL="$gw"; $env:SEAT_SVC_URL="https://$ss"; $env:SEAT_SVC_WS="wss://$ss"
$env:SESSION_ID="e0000000-0000-0000-0000-0000000000aa"; $env:SEAT_MAP_ID="d0000000-0000-0000-0000-0000000000cc"

# 6.2.2 — SeatDesignHub
k6 run k6/seat-design-test/03-signalr-design.js
k6 run -e OBSERVERS=10  k6/seat-design-test/05-broadcast-fanout.js
k6 run -e OBSERVERS=100 k6/seat-design-test/05-broadcast-fanout.js

# 6.2.3 — SeatBookingHub (Redlock)
k6 run -e VUS=500 -e CONTENDED=100 k6/redis-lock-test/redis-lock-test.js
```
