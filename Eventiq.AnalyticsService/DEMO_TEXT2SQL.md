# Demo Text2SQL — Script trình bày

> Chuẩn bị cho buổi bảo vệ. Ngày demo giả định: **2026-06-08**.
> Truy cập: đăng nhập Admin → menu **Statistics** (trang dùng `Text2SqlConsole`).

---

## 0. Chuẩn bị trước khi demo (BẮT BUỘC)

1. **Khởi động service** (Python/FastAPI, port 5238) và toàn bộ backend + frontend.
2. **Chạy "làm nóng" (warm-up)** 2–3 câu hỏi trước khi vào phòng — vì:
   - Groq free-tier có throttle **2.5s giữa mỗi lần gọi LLM**, mỗi câu hỏi gọi **2–3 lần** → mỗi câu mất **~8–15 giây**. Báo trước cho hội đồng "hệ thống đang gọi LLM" để tránh tưởng treo.
   - Tránh lỗi `429 rate limit` ngay lúc demo.
3. **Mở sẵn panel "Generated SQL & Metadata"** (nút mở rộng dưới kết quả) — đây là phần "ăn điểm".

---

## 1. Mở đầu (~30 giây)

> "Phần này em trình bày tính năng **Text2SQL** — cho phép admin/ban tổ chức **đặt câu hỏi bằng tiếng Việt tự nhiên**, hệ thống tự sinh câu lệnh SQL, truy vấn dữ liệu thật trên nhiều microservice, rồi trả về **bảng dữ liệu + biểu đồ phù hợp tự động**. Người dùng không cần biết SQL hay cấu trúc database."

Điểm nhấn một câu: *"Không phải gọi LLM một phát ra SQL, mà là một **pipeline 3 bước có kiểm soát**, dùng đồ thị khoá ngoại để chọn bảng và có cơ chế **tự sửa lỗi SQL**."*

---

## 2. Kiến trúc pipeline (~1 phút — phần kỹ thuật cốt lõi)

Mỗi câu hỏi đi qua 3 bước (3 lần gọi LLM, model **Groq `llama-3.3-70b-versatile`**, `temperature=0` để ổn định):

| Bước | Việc làm | Trả ra |
|---|---|---|
| **1. Entity Extraction** | LLM đọc câu hỏi tiếng Việt + danh sách 25 bảng, trích ra: bảng liên quan, điều kiện lọc, phép gộp, **độ tin cậy (confidence)** | JSON intent |
| **2. Schema Linking** | Chọn đúng bảng + cách JOIN. Nếu `confidence ≥ 0.7` → đi đường **`graph`**: duyệt **đồ thị khoá ngoại (NetworkX)** tìm đường nối ngắn nhất giữa các bảng, tự thêm bảng trung gian và sinh **join hints**. Nếu thấp → **`keyword_fallback`** (xếp hạng bảng theo độ trùng từ khoá) | `relevantTables`, `method` |
| **3. SQL Generation + Self-correction** | Ghép prompt gồm: DDL của **chỉ các bảng liên quan** + join hints + 5 ví dụ mẫu → LLM sinh SQL → chạy. **Nếu Postgres báo lỗi → đưa nguyên thông báo lỗi quay lại LLM sửa 1 lần** rồi chạy lại | SQL, `retries` (0 hoặc 1) |

Sau đó **Chart Picker** (heuristic, không gọi LLM) chọn loại biểu đồ từ hình dạng dữ liệu:
- cột đầu là **ngày/tháng** → `line`
- câu hỏi chứa **"tỉ lệ / phần trăm / %"** → `pie`
- có **≥2 cột số** → `scatter`
- còn lại → `bar`; 1 ô đơn → `table`

**3 điểm để "khoe":**
1. **Cross-service join**: dữ liệu nằm rải ở 5 schema (user / org / event / seat / payment) không có khoá ngoại vật lý giữa các service → hệ thống dùng **logical FK** + đồ thị để tự nối. (VD: doanh thu org = `payment_service.orders.org_id` ↔ `org_service."Organizations"."Id"`.)
2. **Schema linking** chỉ đưa DDL của bảng liên quan vào prompt → prompt gọn, chính xác hơn, đỡ token.
3. **Self-correction**: SQL sai cú pháp/tên cột → tự sửa, thể hiện qua `retries=1`.

---

## 3. Kịch bản demo trực tiếp (chạy theo thứ tự này)

> Với mỗi câu: gõ câu hỏi → chờ ~10s → chỉ vào **biểu đồ**, **bảng dữ liệu**, rồi mở panel **SQL + Metadata** đọc `method` / `relevantTables` / `retries`.

### Câu 1 — Mở màn an toàn (line chart)
**"Doanh thu theo tháng năm nay"**
- Nói: *"Câu đơn giản trên 1 bảng. Để ý nó tự nhận cột tháng → vẽ biểu đồ đường."*
- Kết quả mong đợi: 2 dòng (2026-05, 2026-06), biểu đồ **line**. `method=graph`, bảng `payment_service.orders`.

### Câu 2 — Cross-service join (ăn điểm nhất)
**"Org nào doanh thu cao nhất quý này"**
- Nói: *"Doanh thu nằm ở PaymentService, tên org ở OrgService — hai service khác nhau, không có FK vật lý. Hệ thống tự nối qua logical FK. Xem join hint trong SQL."*
- Kết quả: xếp hạng 3 org theo doanh thu, **bar**. Mở SQL chỉ phần `JOIN ... ON orders.org_id = "Organizations"."Id"`.

### Câu 3 — Biểu đồ tròn (chart picker)
**"Tỉ lệ vé đã check-in của các sự kiện"**
- Nói: *"Từ khoá 'tỉ lệ' → hệ thống chọn biểu đồ tròn. Join 3 bảng event → session → ticket."*
- Kết quả: pie, ~**40% đã check-in** (2/5 vé). `method=graph`.

### Câu 4 — Top N + join nhiều bảng
**"Top 5 sự kiện bán nhiều vé nhất"**
- Nói: *"Gộp + sắp xếp + giới hạn, nối event–session–ticket."*
- Kết quả: hiện sự kiện "Ua Show 1" (5 vé), **bar**. (Lưu ý dữ liệu hiện chỉ 1 sự kiện có vé — vẫn đúng.)

### Câu 5 — (tuỳ chọn) khoe self-correction / câu phức
**"Khách hàng chi tiêu nhiều nhất top 10"** hoặc **"So sánh doanh thu Q1 vs Q2 năm nay"**
- Nói: *"Câu phức hơn, nối User–Order qua service. Nếu SQL đầu lỗi, để ý `retries=1` — hệ thống đã tự sửa."*
- Nếu ra lỗi: biến thành điểm cộng → *"đây là lý do em thiết kế vòng tự sửa; trường hợp này nằm ngoài 1 lần sửa."* rồi chuyển câu khác.

---

## 4. Bộ câu hỏi mẫu (phân theo độ rủi ro)

> ✅ = đã đối chiếu dữ liệu hiện có, chắc chắn ra kết quả. ⚠️ = chạy được nhưng kết quả có thể rỗng/khó đoán với dữ liệu hiện tại.

### Nhóm A — An toàn, nên dùng để demo
| Câu hỏi | Bảng | Biểu đồ | Ghi chú |
|---|---|---|---|
| ✅ Doanh thu theo tháng năm nay | orders | line | khớp ví dụ mẫu |
| ✅ Phí platform đã thu trong tháng | orders | bar/table | June ≈ $3 |
| ✅ Số order theo trạng thái | orders | bar | Paid 6 / Failed 4 |
| ✅ Org nào doanh thu cao nhất quý này | orders + Organizations | bar | cross-service ⭐ |
| ✅ Tỉ lệ vé đã check-in của các sự kiện | events+sessions+tickets | pie | ~40% ⭐ |
| ✅ Top 5 sự kiện bán nhiều vé nhất | events+sessions+tickets | bar | hiện 1 sự kiện |
| ✅ Số lượng staff được mời tham gia mỗi org | Organizations + Invitations | bar | 5 lời mời/3 org |
| ✅ User bị ban gần đây | Users + BanHistories | table | 2 bản ghi |

### Nhóm B — Khoe năng lực, rủi ro nhẹ
| Câu hỏi | Bảng | Ghi chú |
|---|---|---|
| Khách hàng chi tiêu nhiều nhất top 10 | Users + orders | cross-service join |
| So sánh doanh thu Q1 vs Q2 năm nay | orders | logic ngày phức |
| Sự kiện có nhiều session nhất | events + sessions | |
| Doanh thu trung bình mỗi vé | tickets + orders | |
| Legend nào được dùng nhiều nhất trong các sơ đồ ghế | legends + seats | |

### Nhóm C — Tránh dùng với dữ liệu hiện tại (hoặc test kỹ trước)
| Câu hỏi | Lý do |
|---|---|
| ⚠️ Có bao nhiêu user mới tháng này? | **0 dòng** — user mới nhất tạo 31/05, không có ai trong tháng 6. Đổi thành *"Số user đăng ký theo tháng"* hoặc *"trong năm nay"*. |
| ⚠️ Sự kiện đang chờ duyệt | trạng thái event hiện chỉ có 0 và 2 — "chờ duyệt" có thể rỗng |
| ⚠️ Tỉ lệ ghế VIP đã bán / Org nào chưa connect Stripe / Số ghế template chưa publish | đi đường `keyword_fallback`, không có join hint → dễ cần retry hoặc lỗi |

---

## 5. Câu hỏi hội đồng có thể hỏi (chuẩn bị trả lời trung thực)

**Q: Sinh SQL sai thì sao?**
→ Có vòng **tự sửa 1 lần**: đưa thông báo lỗi của Postgres ngược lại LLM để sửa. Thể hiện qua `retries`. Nếu vẫn lỗi → trả thông báo lỗi rõ ràng, không trả dữ liệu sai.

**Q: Làm sao chọn đúng bảng trong số rất nhiều bảng?**
→ Không nhồi toàn bộ schema vào prompt. Bước **Schema Linking** dùng **đồ thị khoá ngoại** chọn ra bảng + đường JOIN liên quan, chỉ đưa DDL của các bảng đó vào → chính xác và tiết kiệm token.

**Q: Chống SQL injection / câu lệnh phá hoại (DROP/DELETE)?**
→ *Trung thực:* hiện prompt yêu cầu "chỉ SELECT" nhưng **chưa có lớp chặn cứng** và connection đang chạy bằng role chủ DB. **Hướng phát triển:** dùng **role chỉ-đọc (read-only)**, chặn từ khoá ghi/DDL, và bọc trong transaction read-only. *(Nên nói trước như một limitation đã nhận biết, đừng để bị bắt.)*

**Q: Phân quyền — admin xem tất cả, ban tổ chức chỉ xem org mình?**
→ *Trung thực:* service Text2SQL hiện chạy truy vấn **toàn cục** (chưa gắn JWT/scoping vào endpoint này). Phần phân quyền theo org (role Neon hạn chế + view `org_analytics` + biến phiên GUC + RLS) là **thiết kế đã có nhưng chưa wire vào service Python này**. → Nếu hội đồng hỏi, trình bày như **đã thiết kế, là bước hoàn thiện tiếp theo**; **đừng khẳng định nó đang chạy** ở đây (frontend có ghi chú nói vậy nhưng backend chưa thực thi).

**Q: Vì sao chậm vài giây?**
→ Free-tier Groq có throttle giữa các lần gọi; mỗi câu gọi LLM 2–3 lần. Bản trả phí / self-host sẽ nhanh hơn nhiều.

---

## 6. Số liệu dữ liệu hiện có (để trích dẫn khi demo)

- **Orders:** 10 (6 Paid, 4 Failed) — doanh thu Paid ≈ **$60**, phí platform ≈ **$3**; `paid_at` trải 31/05 → 08/06/2026.
- **Events:** 5 (2 Approved, 3 nháp). Chỉ "Ua Show 1" có vé.
- **Tickets:** 5 — **2 đã check-in (40%)**.
- **Orgs:** 3 · **Members:** 4 · **Invitations:** 5 · **Users:** 5 (tạo 22/03–31/05/2026) · **BanHistories:** 2.
- **Seats:** 139 · **Seat maps:** 5.
