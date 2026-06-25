# Seed Plan — Full Demo Dataset

Mục tiêu: seed một bộ dữ liệu **đầy đủ, thực tế** cho demo/defense, mở rộng từ `data/seed_demo.sql`
(hiện chỉ có 1 org + 20 event) lên quy mô lớn, bao gồm cả doanh thu / vé / check-in.

> **Phạm vi DB**: script nhắm tới setup **một database Neon duy nhất chứa nhiều schema**
> (`user_service`, `org_service`, `event_service`, `seat_service`, `payment_service`) —
> giống `seed_demo.sql` hiện tại. Với prod database-per-service (5 DB riêng) thì phải tách
> thành 5 script chạy trên từng connection (xem Phase 7).

---

## 1. Số lượng mục tiêu (đã chốt)

| Nhóm | Số lượng | Ghi chú |
|------|----------|---------|
| **Organizations** | 20 | mỗi org 1 owner riêng |
| **Owner users** | 20 | login được, mật khẩu chung `Demo@123` |
| **Buyer users** | ~80 | pool người mua vé cho orders |
| **Events** | **100** | phân bổ bên dưới |
| → past | 20 | `start_time` < 16/06/2026, status **Approved** |
| → current (cửa sổ A) | 35 | `start_time` ∈ [17/06/2026, 27/06/2026], status **Approved** |
| → current (cửa sổ B) | 35 | `start_time` ∈ [27/06/2026, 10/07/2026], status **Approved** |
| → draft | 10 | status **Draft**, không hiện public |
| **Sessions** | ~120 | 1–2 suất/event |
| **Legends** | ~250 | 2–3 hạng giá/event (Standard/VIP/Premium) |
| **Seat maps + seats** | ~90 maps | cho 70 current + 20 past (sellable); draft không có |
| **Orders + tickets** | nhiều nghìn | doanh thu **đậm** (xem Phase 6) |

`100 = 20 + 35 + 35 + 10`. Hôm nay = **2026-06-16**.

---

## 2. Hằng số schema đã xác minh

### Enum
- `EventStatus`: `Draft=0, Pending=1, Approved=2, Rejected=3, Published=4, Cancelled=5`
  - **Public `/events` lọc `status='Approved'` (=2)** → tất cả event muốn hiện phải là **2**, KHÔNG phải 4.
    (Seed cũ dùng 4/Published nên sẽ không hiện trên trang explore hiện tại.)
  - Draft = **0**.
- `PaymentStatus` (org): `NotConfigured=0, Pending=1, Configured=2`. Org bán được vé → **2**.
- `SeatStatus` (lưu **text**): `Available`, `Holding`, `Sold`, `Blocked`. Ghế đã bán → `'Sold'`.
- `AppRoles`: `Admin, User, Staff, Organization` (Role seed lúc startup, ID random → tra theo `Name`).

### Mật khẩu
- Hash = `Base64(SHA256(utf8(password)))` (KHÔNG phải BCrypt).
- `Demo@123` → **`/5ZnMgXcciMgWY6/j4gyWyrFaSLVohZLV2WGgnS8DXM=`**

### Province (lấy từ API ngoài, `code` 2 chữ số — UI gửi đúng `code` này)
```
01 Thành phố Hà Nội      48 Thành phố Đà Nẵng     79 Thành phố Hồ Chí Minh
31 Thành phố Hải Phòng   46 Thành phố Huế          92 Thành phố Cần Thơ
22 Tỉnh Quảng Ninh       56 Tỉnh Khánh Hòa         68 Tỉnh Lâm Đồng
40 Tỉnh Nghệ An          75 Tỉnh Đồng Nai
```
(Có thể fetch full list từ `https://production.cas.so/address-kit/2025-07-01/provinces` để lấy thêm
`commune_code` thật; nếu không, dùng commune giả `'00000'` + tên quận tượng trưng.)

### Owner role wiring (QUAN TRỌNG)
Role `Organization` của owner bình thường do MassTransit consumer (`OrganizationCreated`) tạo.
Seed bằng SQL **bỏ qua** message bus → phải **tự insert `user_service."UserRoles"`** cho owner:
- 1 dòng role `User` (OrganizationId = NULL)
- 1 dòng role `Organization` (OrganizationId = org của họ)

### Cột bảng (theo `seed_demo.sql` đã chạy được)
- `org_service."Organizations"`: Id, Name, Description, OwnerId, OwnerEmail, StripeAccountId, PaymentStatus, PaymentConfiguredAt, CreatedAt, UpdatedAt, IsDeleted
- `org_service."Permissions"`: Id, Name, OrganizationId, IsDesigner, CreatedAt, IsDeleted
- `org_service."Members"`: Id, UserId, Email, OrganizationId, PermissionId, CreatedAt, IsDeleted
- `event_service.org_payment_infos`: id, organization_id, stripe_account_id, is_active, updated_at, created_at, is_deleted
- `event_service.events`: id, organization_id, organization_name, name, description, detail_address, province_code, commune_code, province_name, commune_name, event_banner, status, start_time, end_time, created_at, updated_at, is_deleted
- `event_service.charts`: id, name, event_id, created_at, updated_at, is_deleted
- `event_service.sessions`: id, event_id, name, start_time, end_time, chart_id, created_at, updated_at, is_deleted
- `event_service.legends`: id, name, color, price, event_id, created_at, updated_at, is_deleted
- `event_service.tickets`: id, order_id, session_id, seat_id, seat_label, legend_name, price, qr_code, is_checked_in, checked_in_at, issued_at, created_at, updated_at, is_deleted
- `seat_service.seat_maps`: id, chart_id, event_id, organization_id, session_id, name, status, canvas_settings, version, total_seats, created_at, updated_at, is_deleted
- `seat_service.seats`: id, seat_map_id, label, seat_number, status, seat_type, position(jsonb), legend_id, custom_properties, held_by, held_until, created_at, updated_at, is_deleted
- `payment_service.orders`: id, user_id, org_id, session_id, stripe_session_id, status, total_amount, platform_fee, event_name, session_name, session_date, paid_at, created_at, updated_at, is_deleted
- `payment_service.order_items`: id, order_id, seat_id, seat_label, legend_name, price, created_at, updated_at, is_deleted

> **TODO trước khi code**: xác minh full cột `user_service."Users"` (ngoài Id/Email/Username/PasswordHash/Avatar/CreatedAt
> còn UpdatedAt/IsDeleted/Phone/IsBanned…?) và `"UserRoles"`/`"Roles"`. Chỉ migration đã thấy một phần.

---

## 3. Quy ước ID & idempotency

- Dùng **UUID tiền tố cố định** theo loại để dễ nhận diện & teardown, re-run an toàn (`ON CONFLICT DO NOTHING`):
  - owner user:  `b0000000-0000-0000-0000-0000000000NN` (NN = 01..20)
  - buyer user:  `b0000000-0000-0000-0001-0000000000NN`
  - org:         `a0000000-0000-0000-0000-0000000000NN`
  - permission:  `a0000000-0000-0000-0001-0000000000NN`
- Event/session/seat/order ID: `gen_random_uuid()` (volume lớn) nhưng gắn **marker nhận diện**:
  - event name **không** có prefix (cần trông thật) → nhận diện qua `organization_id IN (20 org seed)`.
  - order: `stripe_session_id LIKE 'cs_seed_%'`; ticket: `qr_code LIKE 'QR-SEED-%'`.
- Toàn bộ chạy trong `DO $$ ... $$` blocks + arrays, giống `seed_demo.sql`.

---

## 4. Pool metadata (để event trông thật)

- **Tên org** (20): trộn từ {"Sài Gòn", "Hà Nội", "Đà Nẵng"...} × {"Live Nation", "Entertainment",
  "Media", "Events", "Production", "Group"} → vd "Saigon Live Nation", "Hanoi Music Group".
- **Chủ đề event**: Âm nhạc / Hội thảo / Thể thao / Workshop / Lễ hội / Triển lãm / Stand-up.
  Tên ghép `{chủ đề} + {tính từ} + {năm/mùa}` → vd "Đêm Nhạc Acoustic Mùa Hè 2026".
- **Banner**: `https://picsum.photos/seed/eq-<slug>/800/400` (ổn định, không cần asset).
- **Mô tả / địa chỉ**: template + tên tỉnh/thành.
- **Hạng vé**: Standard `#6366f1`, VIP `#f59e0b`, Premium `#ec4899` với giá tăng dần
  (150k–500k / 400k–1.2tr / 900k–2tr), random trong khoảng.

---

## 5. Các phase build script

### Phase 1 — Reference & pools
Khai báo arrays: provinces (code+name), banner slugs, theme words, org names, customer names.
Tra Role IDs theo tên (`User`, `Organization`).

### Phase 2 — Users & roles
- Insert 20 **owner users** (ID cố định, email `owner01@eventiq.dev`…, hash chung, role `User`+`Organization`).
- Insert ~80 **buyer users** (ID cố định, email `buyer001@eventiq.dev`…, hash chung, role `User`).

### Phase 3 — Organizations
- 20 `Organizations` (OwnerId = owner tương ứng, PaymentStatus=2 cho ~18 org, 2 org để `NotConfigured` cho đa dạng).
- Mỗi org: 1 `Permissions` (Staff, IsDesigner=true) + 1–3 `Members` (owner + vài staff email).
- `org_payment_infos` (event_service) cho mỗi org có payment Configured.
- `UserRole(Organization, OrgId)` cho owner (đã nêu Phase 2).

### Phase 4 — Events (vòng lặp chính)
Phân bổ 100 event vòng tròn qua 20 org (mỗi org ~5 event, trộn nhóm).
Mỗi event sinh: 1 chart, 1–2 session (trong khoảng start/end), 2–3 legend.
**Status & ngày theo nhóm:**
- past (20): `status=2`, `start_time` rải đều 12 tháng trước 16/06/2026.
- current A (35): `status=2`, `start_time` random ∈ [2026-06-17, 2026-06-27].
- current B (35): `status=2`, `start_time` random ∈ [2026-06-27, 2026-07-10].
- draft (10): `status=0`, `start_time` tương lai (sau 10/07/2026).
`end_time` = `start_time` + 2–6h (hoặc nhiều ngày cho festival).

### Phase 5 — Seat maps & seats
Cho **70 current + 20 past** (draft bỏ qua):
- 1 `seat_maps` (status `'Published'`, total_seats random 80–500) gắn chart+session.
- Sinh seats bằng `generate_series`, `position` jsonb lưới, gán `legend_id` theo tỉ lệ
  (70% Standard / 20% VIP / 10% Premium). Mặc định `status='Available'`.

### Phase 6 — Sales / revenue (ĐẬM)
Với mỗi event **sellable** sinh orders+order_items+tickets (và update seat→`'Sold'`):
- **past (20)**: bán **60–85%** số ghế. `~70%` ticket `is_checked_in=true` (`checked_in_at` quanh giờ diễn).
  `paid_at` rải đều ~30 ngày trước `start_time`. → nguồn chính cho biểu đồ doanh thu lịch sử.
- **current (70)**: bán **20–50%** số ghế, **chưa** check-in (sự kiện chưa diễn). `paid_at` gần hiện tại.
- Mỗi order: `user_id` = random buyer pool, `status='Paid'`, `total_amount`=giá legend của ghế,
  `platform_fee`=5%. `order_items` + `tickets` khớp seat_id thật (ghế vừa set `'Sold'`).
- `stripe_session_id='cs_seed_<8hex>'`, `qr_code='QR-SEED-<...>'`.
> Ước lượng volume "đậm": ~6–10k orders, ~6–10k tickets, ~18k seats. OK với Postgres/Neon.

### Phase 7 — Verify & teardown
- Khối `SELECT ... UNION ALL` đếm theo nhóm (orgs, users, events theo status, seat_maps, seats sold,
  orders Paid, tickets, tickets checked-in) — xác nhận đúng số.
- Khối teardown (comment sẵn) xoá theo marker: tickets QR-SEED → order_items/orders cs_seed →
  seats/seat_maps của 20 org → legends/sessions/charts/events của 20 org → members/permissions/orgs →
  user_roles/users seed.
- **Prod multi-DB**: tách file theo service, chạy từng connection; bỏ FK cross-schema ngầm
  (đã không có FK cross-service nên chỉ cần đúng thứ tự).

---

## 6. Cách chạy (dự kiến)
```bash
# Prereq: chạy services 1 lần để UserService seed Roles (Admin/User/Staff/Organization).
psql "<neon-conn>" -f data/seed_full.sql
# Idempotent: chạy lại an toàn (ON CONFLICT DO NOTHING).
# Login demo: owner01@eventiq.dev .. owner20@eventiq.dev  /  Demo@123
```

---

## 7. Rủi ro / điểm cần xác nhận
1. **Full cột `Users`/`UserRoles`** chưa verify hết (TODO Phase 2) — phải đọc entity/migration trước khi code.
2. **`commune_code` thật**: nếu cần filter theo phường/xã thì phải fetch communes từng tỉnh; demo có thể bỏ.
3. **Status Approved vs Published**: cần xác nhận lại business có cho phép book khi mới Approved (chưa Published)
   không — nếu booking yêu cầu Published thì past/current event muốn vừa hiện list vừa book được sẽ cần
   thêm bước (hoặc set Published và sửa filter `/events` để nhận cả Approved+Published).
4. **Org chưa config payment (2 org NotConfigured)**: không seed event sellable cho 2 org đó (hoặc seed event draft).
```
```
