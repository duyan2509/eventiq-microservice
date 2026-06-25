# Material viết mục 6.2 — Kiểm thử hiệu năng (load test)

> Thông số rút trực tiếp từ script k6 trong `eventiq-microservice/k6/`. Số kết quả (6.2.2/6.2.3)
> điền sau khi chạy (cần cài k6 + hệ thống đang chạy + đã seed `data/seed_load_test_data.sql`).

---

## 6.2.1. Phương pháp và công cụ kiểm thử

### Công cụ kiểm thử

Hệ thống EventIQ được kiểm thử tải bằng **Grafana k6** (phiên bản 0.55+) — công cụ kiểm thử hiệu năng mã nguồn mở viết bằng Go, kịch bản viết bằng JavaScript/ES6. k6 được lựa chọn vì các lý do sau:

- **Hỗ trợ WebSocket gốc:** cần thiết để mô phỏng kết nối SignalR thật sự (HTTP negotiate → WebSocket upgrade → SignalR binary framing), điều mà JMeter hoặc Locust cần plugin bổ sung.
- **Mô hình Virtual User (VU) sát thực tế:** mỗi VU duy trì trạng thái riêng (JWT token, connection ID, seat ID) và thực thi luồng hành vi đầy đủ như người dùng thật, khác với mô hình request/s thuần túy.
- **Threshold tích hợp:** định nghĩa tiêu chí PASS/FAIL trực tiếp trong script (ví dụ `p(95) < 2000ms`); k6 trả về exit code khác 0 nếu vi phạm, dễ tích hợp vào pipeline.
- **Nhẹ và không phụ thuộc JVM:** một binary duy nhất, không cần server riêng như JMeter, thích hợp chạy từ máy cục bộ gửi tải lên cloud.

Để so sánh: JMeter mạnh ở giao thức đa dạng nhưng nặng về tài nguyên và cú pháp XML khó bảo trì; Locust (Python) hỗ trợ WebSocket kém hơn và overhead GIL ảnh hưởng tới độ chính xác khi VU lớn.

### Môi trường kiểm thử

Toàn bộ kiểm thử thực hiện trên **môi trường cloud — Azure Container Apps (ACA)**, phản ánh đúng điều kiện vận hành thực tế của hệ thống.

**Kiến trúc triển khai:**

| Thành phần | Loại ingress | CPU | RAM | Min / Max replica |
|---|---|---|---|---|
| `api-gateway` (Ocelot) | External (public) | 0.5 vCPU | 1.0 Gi | 1 / 2 |
| `seat-service` (SignalR + Redlock) | External (public) | 0.5 vCPU | 1.0 Gi | 1 / 3 |
| `user-service` | Internal | 0.5 vCPU | 1.0 Gi | 1 / 2 |
| `org-service`, `event-service`, `payment-service` | Internal | 0.5 vCPU | 1.0 Gi | 1 / 2 |
| `email-service` | Không có (consumer) | 0.5 vCPU | 1.0 Gi | 1 / 2 |

> `seat-service` được cấu hình **sticky sessions** (session affinity) để mỗi kết nối SignalR luôn định tuyến tới cùng một replica, đảm bảo tính nhất quán của `seat:presence` trên Redis.

**Hạ tầng managed:**
- **Cơ sở dữ liệu:** Azure Database for PostgreSQL Flexible Server — 5 database độc lập cho 5 service.
- **Cache / Distributed Lock:** Azure Cache for Redis — dùng cho output cache (`seat:presence`, `seat:selection`) và Redlock phân tán.
- **Message broker:** Azure Service Bus — thay thế RabbitMQ trong môi trường production.
- **Container Registry:** Azure Container Registry (ACR) — lưu trữ Docker image đã build.

**Máy phát tải (k6 client):** `[CPU ___, RAM ___, OS Windows 11]` — k6 chạy cục bộ và gửi tải tới endpoint HTTPS của ACA. Giá trị độ trễ đo được bao gồm cả round-trip mạng client → cloud (thường 10–30 ms tùy vị trí).

### Dữ liệu nền (Seed data)

Trước khi chạy bất kỳ kịch bản nào, script `data/seed_load_test_data.sql` được thực thi trên database cloud để tạo bộ dữ liệu cố định với các UUID xác định trước:

| Thực thể | UUID cố định |
|---|---|
| Organization | `a0000000-0000-0000-0000-000000000001` |
| Event | `b0000000-0000-0000-0000-000000000001` |
| Event Session | `e0000000-0000-0000-0000-0000000000aa` |
| SeatMap (Draft) | `d0000000-0000-0000-0000-0000000000cc` |

Việc dùng UUID cố định đảm bảo tính tái lập (reproducibility) — mỗi lần chạy không cần tạo lại dữ liệu và kết quả có thể so sánh giữa các lần.

### Tổng quan các kịch bản kiểm thử

Bộ kiểm thử gồm **4 script k6** bao phủ hai phân hệ chịu tải cao nhất của hệ thống:

| # | Script | Phân hệ kiểm tra | Giao thức | Mô hình tải | VU đỉnh | Thời gian |
|---|---|---|---|---|---|---|
| 01 | `seat-design-test/01-layout-cache.js` | SeatService — Redis output cache | HTTP/REST | Ramping VUs (warmup 1 VU → 100 VU) | 100 | ~61 s |
| 02 | `seat-design-test/02-seat-api.js` | SeatService — REST CRUD | HTTP/REST | Ramping VUs (0 → 50 VU) | 50 | ~50 s |
| 03 | `seat-design-test/03-signalr-design.js` | **SeatDesignHub** (SignalR) | WebSocket | Ramping VUs (0 → 10 → 20 → 0) | **20** | ~50 s |
| 04 | `redis-lock-test/redis-lock-test.js` | **SeatBookingHub** (Redlock) | HTTP/REST | Per-VU-iterations (1 lần / VU) | **500** | ≤ 60 s |

### Chiến lược đo lường và ngưỡng đánh giá

Mỗi script định nghĩa **custom metrics** và **thresholds** tích hợp — k6 tự động đánh giá PASS/FAIL:

**Kịch bản 01 — Redis output cache:**

| Metric | Ngưỡng |
|---|---|
| `http_req_duration` p95 | < 300 ms |
| `http_req_duration` p99 | < 500 ms |
| `layout_req_duration` p95 | < 200 ms |
| `layout_error_rate` | < 1% |

**Kịch bản 02 — REST CRUD:**

| Metric | Ngưỡng |
|---|---|
| `http_req_duration` p95 | < 800 ms |
| `seatmap_create_duration` p95 | < 600 ms |
| `seatmap_read_duration` p95 | < 300 ms |
| `seatmap_error_rate` | < 2% |

**Kịch bản 03 — SeatDesignHub (SignalR):**

| Metric | Ngưỡng |
|---|---|
| `signalr_connect_duration` p95 | < 2 000 ms |
| `signalr_join_duration` p95 | < 3 000 ms |
| `signalr_error_rate` | < 5% |

**Kịch bản 04 — Redlock (thí nghiệm cốt lõi):**

| Metric | Ngưỡng (kỳ vọng) | Ý nghĩa |
|---|---|---|
| `lock_success` | ≤ 100 | Không bao giờ vượt số ghế tranh chấp |
| `lock_conflict` | ≥ 400 | Phần lớn yêu cầu bị từ chối đúng cách |
| `lock_other` | = 0 | Không có lỗi ngoài dự kiến (5xx…) |

Ngoài kết quả terminal, mỗi script xuất file JSON vào thư mục `k6/results/` để lưu trữ và phân tích sau.

---

## 6.2.2. Kiểm thử tải phân hệ thiết kế sơ đồ (SeatDesignHub)

**Kịch bản** (`03-signalr-design.js`): tải tăng dần **0 → 10 → 20 VU** (10s + 30s + 10s, ~50s), mỗi VU là
một người thiết kế đồng thời, thực hiện đúng luồng thật qua SignalR:
negotiate → handshake → `JoinSeatMap` (nhận `CurrentPresence`) → `AddSeat` → `UpdateSeats` (di chuyển ghế)
→ `SendCursorPosition` ×3 → `DeleteSeats` + `LeaveSeatMap` → đóng kết nối.
Chứng minh: Redis `seat:presence:{seatMapId}` tăng theo số VU; hub xử lý ghi ghế đồng thời không race.

*(Bộ test còn: 01-layout-cache 100 VU đo throughput output-cache; 02-seat-api 50 VU REST CRUD; 04-viewport so sánh tải theo viewport.)*

**Bảng 6.2 — Kết quả SeatDesignHub** *(điền sau khi chạy)*
| Chỉ số | Giá trị |
|---|---|
| Số VU đỉnh | 20 |
| Connect p95 (ms) | ___ |
| JoinSeatMap p95 (ms) | ___ |
| Tổng message nhận | ___ |
| Error rate (%) | ___ |
| Kết luận ngưỡng (PASS/FAIL) | ___ |

---

## 6.2.3. Kiểm thử tải phân hệ đặt chỗ/giữ ghế (SeatBookingHub — Redlock)

**Kịch bản** (`redis-lock-test.js`) — **thí nghiệm cốt lõi của luận văn**: N=100 ghế bị tranh chấp bởi
**500 VU đồng thời**, mỗi VU gửi đúng 1 yêu cầu `POST /seat-maps/{id}/seats/hold` cho ghế
`seatIds[(VU-1) % N]` → **mỗi ghế bị 5 VU giành cùng lúc**.
- Redlock BẬT → đúng **N** request `200 OK`, phần còn lại `409 Conflict`, **không có Holding trùng**.
- Redlock TẮT → over-sell: >N thành công / Holding trùng (race condition).

**Kiểm chứng ở DB sau khi chạy:**
```sql
SELECT count(*) FROM seat_service.seats
WHERE seat_map_id='<clone map>' AND status='Holding';   -- phải = N (100)
```

**Bảng 6.3 — Kết quả Redlock** *(điền sau khi chạy)*
| Chỉ số | Kỳ vọng | Thực tế |
|---|---|---|
| Ghế tranh chấp (N) | 100 | 100 |
| VU đồng thời | 500 | 500 |
| `200 OK` (giữ thành công) | 100 | ___ |
| `409 Conflict` | 400 | ___ |
| Bất thường (khác 200/409) | 0 | ___ |
| Hold p95 (ms) | — | ___ |
| Holding trong DB | 100 | ___ |
| **Kết luận** | no over-sell | ___ |

---

## Cách chạy (khi có k6 + hệ thống chạy)

**1. Cài k6** (Windows, chọn 1):
```powershell
winget install k6           # hoặc: winget install --id Grafana.k6
# hoặc Chocolatey:  choco install k6
# hoặc tải binary: https://github.com/grafana/k6/releases
```

**2. Seed dữ liệu** (1 lần): chạy `data/seed_load_test_data.sql` lên DB (tạo org/event/session/seat_map ID cố định + đủ ghế Available).

**3. Chạy test trỏ vào ACA** (từ thư mục `eventiq-microservice/k6/`) — BẮT BUỘC override endpoint cloud:
```powershell
# đặt endpoint ACA 1 lần (PowerShell)
$GW  = "https://<gateway-app>.<region>.azurecontainerapps.io/gateway"
$SEAT= "https://<seat-app>.<region>.azurecontainerapps.io"
$WS  = "wss://<seat-app>.<region>.azurecontainerapps.io"

# SeatDesignHub
k6 run -e BASE_URL=$GW -e SEAT_SVC_URL=$SEAT -e SEAT_SVC_WS=$WS seat-design-test/03-signalr-design.js

# Redlock — thí nghiệm chính
k6 run -e BASE_URL=$GW -e SEAT_SVC_URL=$SEAT -e CONTENDED=100 -e VUS=500 redis-lock-test/redis-lock-test.js
```
k6 in summary ra console + lưu JSON vào `results/`. Lấy số điền Bảng 6.2/6.3.

> Lưu ý SignalR qua gateway: nếu hub không expose trực tiếp mà đi qua Ocelot, `SEAT_SVC_URL/WS` phải trỏ
> route hub tương ứng trên gateway (kiểm tra `ocelot.json`).
