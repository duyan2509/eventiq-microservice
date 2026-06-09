"""One-shot, idempotent: append the Phase-3 expansion questions (024-083)
to dataset.json. Re-running only adds ids not already present.
Run gold SQL through verify_dataset.py afterwards."""
from __future__ import annotations

import json
from pathlib import Path

DATASET = Path(__file__).resolve().parent / "dataset.json"

NEW = [
    # ───────────────────────── EASY (single table) ─────────────────────────
    ("024", "easy", "Tổng số tổ chức trên hệ thống", ["org"],
     '''SELECT COUNT(*) AS total_orgs FROM org_service."Organizations" WHERE NOT "IsDeleted";'''),
    ("025", "easy", "Số sự kiện đã publish", ["event"],
     '''SELECT COUNT(*) AS published FROM event_service.events WHERE status = 4 AND NOT is_deleted;'''),
    ("026", "easy", "Số ghế còn trống", ["seat"],
     '''SELECT COUNT(*) AS available_seats FROM seat_service.seats WHERE status = 'Available';'''),
    ("027", "easy", "Tổng số vé đã phát hành", ["event"],
     '''SELECT COUNT(*) AS tickets FROM event_service.tickets;'''),
    ("028", "easy", "Số đơn hàng theo trạng thái", ["payment"],
     '''SELECT status, COUNT(*) AS n FROM payment_service.orders GROUP BY status ORDER BY n DESC;'''),
    ("029", "easy", "Số user đang bị ban", ["user"],
     '''SELECT COUNT(*) AS banned FROM user_service."Users" WHERE "IsBanned" = TRUE;'''),
    ("030", "easy", "Số sự kiện theo từng trạng thái", ["event"],
     '''SELECT status, COUNT(*) AS n FROM event_service.events GROUP BY status ORDER BY status;'''),
    ("031", "easy", "Số ghế theo từng loại ghế", ["seat"],
     '''SELECT seat_type, COUNT(*) AS n FROM seat_service.seats GROUP BY seat_type ORDER BY seat_type;'''),
    ("032", "easy", "Số lời mời theo trạng thái", ["org"],
     '''SELECT "Status", COUNT(*) AS n FROM org_service."Invitations" GROUP BY "Status" ORDER BY "Status";'''),
    ("033", "easy", "Tổng doanh thu toàn hệ thống", ["payment"],
     '''SELECT SUM(total_amount) AS revenue FROM payment_service.orders WHERE status = 'Paid';'''),
    ("034", "easy", "Tổng số session đã tạo", ["event"],
     '''SELECT COUNT(*) AS sessions FROM event_service.sessions WHERE NOT is_deleted;'''),
    ("035", "easy", "Mức phí hiện tại của nền tảng", ["org"],
     '''SELECT "CurrentFeeRate" FROM org_service."PlatformConfigs" ORDER BY "Id" LIMIT 1;'''),
    ("036", "easy", "Số tổ chức đã kết nối Stripe", ["org"],
     '''SELECT COUNT(*) AS active FROM org_service."Organizations" WHERE "PaymentStatus" = 2;'''),
    ("037", "easy", "Số vé đã check-in", ["event"],
     '''SELECT COUNT(*) AS checked_in FROM event_service.tickets WHERE is_checked_in = TRUE;'''),
    ("038", "easy", "Số sơ đồ ghế đã publish", ["seat"],
     '''SELECT COUNT(*) AS published_maps FROM seat_service.seat_maps WHERE status = 'Published';'''),
    ("039", "easy", "Giá vé trung bình trên toàn bộ loại vé", ["event"],
     '''SELECT AVG(price) AS avg_price FROM event_service.legends;'''),
    ("040", "easy", "Số đơn hàng đã thanh toán hôm nay", ["payment"],
     '''SELECT COUNT(*) AS paid_today FROM payment_service.orders WHERE status = 'Paid' AND paid_at >= DATE_TRUNC('day', CURRENT_DATE);'''),

    # ───────────────────── MEDIUM (2-3 services, join+agg) ─────────────────────
    ("041", "medium", "Doanh thu theo từng tổ chức", ["org", "payment"],
     '''SELECT org."Name", SUM(o.total_amount) AS revenue FROM org_service."Organizations" org JOIN payment_service.orders o ON o.org_id = org."Id" WHERE o.status = 'Paid' GROUP BY org."Id", org."Name" ORDER BY revenue DESC;'''),
    ("042", "medium", "Số vé bán theo từng sự kiện", ["event"],
     '''SELECT e.name, COUNT(t.id) AS tickets FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN event_service.tickets t ON t.session_id = s.id GROUP BY e.id, e.name ORDER BY tickets DESC;'''),
    ("043", "medium", "Top 10 sự kiện doanh thu cao nhất", ["event", "payment"],
     '''SELECT e.name, SUM(o.total_amount) AS revenue FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN payment_service.orders o ON o.session_id = s.id WHERE o.status = 'Paid' GROUP BY e.id, e.name ORDER BY revenue DESC LIMIT 10;'''),
    ("044", "medium", "Số ghế đã bán theo từng tổ chức", ["seat"],
     '''SELECT sm.organization_id, COUNT(*) AS sold FROM seat_service.seat_maps sm JOIN seat_service.seats st ON st.seat_map_id = sm.id WHERE st.status = 'Sold' GROUP BY sm.organization_id ORDER BY sold DESC;'''),
    ("045", "medium", "Doanh thu theo từng loại vé", ["payment"],
     '''SELECT oi.legend_name, SUM(oi.price) AS revenue FROM payment_service.order_items oi JOIN payment_service.orders o ON o.id = oi.order_id WHERE o.status = 'Paid' GROUP BY oi.legend_name ORDER BY revenue DESC;'''),
    ("046", "medium", "Số đơn hàng của mỗi khách hàng", ["user", "payment"],
     '''SELECT u."Username", COUNT(o.id) AS orders FROM user_service."Users" u JOIN payment_service.orders o ON o.user_id = u."Id" GROUP BY u."Id", u."Username" ORDER BY orders DESC;'''),
    ("047", "medium", "Số sự kiện của mỗi tổ chức", ["org", "event"],
     '''SELECT org."Name", COUNT(e.id) AS events FROM org_service."Organizations" org JOIN event_service.events e ON e.organization_id = org."Id" GROUP BY org."Id", org."Name" ORDER BY events DESC;'''),
    ("048", "medium", "Tỉ lệ ghế đã bán của mỗi sơ đồ ghế", ["seat"],
     '''SELECT seat_map_id, COUNT(*) FILTER (WHERE status = 'Sold') * 1.0 / COUNT(*) AS sold_ratio FROM seat_service.seats GROUP BY seat_map_id ORDER BY sold_ratio DESC;'''),
    ("049", "medium", "Giá trị đơn hàng trung bình theo tổ chức", ["org", "payment"],
     '''SELECT org."Name", AVG(o.total_amount) AS avg_order FROM org_service."Organizations" org JOIN payment_service.orders o ON o.org_id = org."Id" WHERE o.status = 'Paid' GROUP BY org."Id", org."Name" ORDER BY avg_order DESC;'''),
    ("050", "medium", "Số vé check-in theo từng sự kiện", ["event"],
     '''SELECT e.name, COUNT(*) FILTER (WHERE t.is_checked_in) AS checked_in FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN event_service.tickets t ON t.session_id = s.id GROUP BY e.id, e.name ORDER BY checked_in DESC;'''),
    ("051", "medium", "Top 5 khách hàng có nhiều đơn nhất", ["user", "payment"],
     '''SELECT u."Username", COUNT(o.id) AS orders FROM user_service."Users" u JOIN payment_service.orders o ON o.user_id = u."Id" WHERE o.status = 'Paid' GROUP BY u."Id", u."Username" ORDER BY orders DESC LIMIT 5;'''),
    ("052", "medium", "Tổng số ghế của mỗi sự kiện", ["event", "seat"],
     '''SELECT e.name, COUNT(st.id) AS seats FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN seat_service.seat_maps sm ON sm.session_id = s.id JOIN seat_service.seats st ON st.seat_map_id = sm.id GROUP BY e.id, e.name ORDER BY seats DESC;'''),
    ("053", "medium", "Doanh thu theo tỉnh thành", ["event", "payment"],
     '''SELECT e.province_name, SUM(o.total_amount) AS revenue FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN payment_service.orders o ON o.session_id = s.id WHERE o.status = 'Paid' GROUP BY e.province_name ORDER BY revenue DESC;'''),
    ("054", "medium", "Số session của mỗi sự kiện", ["event"],
     '''SELECT e.name, COUNT(s.id) AS sessions FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id GROUP BY e.id, e.name ORDER BY sessions DESC;'''),
    ("055", "medium", "Tổng phí nền tảng thu được theo tổ chức", ["org", "payment"],
     '''SELECT org."Name", SUM(o.platform_fee) AS fees FROM org_service."Organizations" org JOIN payment_service.orders o ON o.org_id = org."Id" WHERE o.status = 'Paid' GROUP BY org."Id", org."Name" ORDER BY fees DESC;'''),
    ("056", "medium", "Số loại vé của mỗi sự kiện", ["event"],
     '''SELECT e.name, COUNT(l.id) AS legends FROM event_service.events e JOIN event_service.legends l ON l.event_id = e.id GROUP BY e.id, e.name ORDER BY legends DESC;'''),
    ("057", "medium", "Số đơn hàng thất bại theo tổ chức", ["org", "payment"],
     '''SELECT org."Name", COUNT(o.id) AS failed FROM org_service."Organizations" org JOIN payment_service.orders o ON o.org_id = org."Id" WHERE o.status = 'Failed' GROUP BY org."Id", org."Name" ORDER BY failed DESC;'''),
    ("058", "medium", "Doanh thu theo tháng của từng tổ chức", ["org", "payment"],
     '''SELECT org."Name", DATE_TRUNC('month', o.paid_at) AS month, SUM(o.total_amount) AS revenue FROM org_service."Organizations" org JOIN payment_service.orders o ON o.org_id = org."Id" WHERE o.status = 'Paid' GROUP BY org."Id", org."Name", 2 ORDER BY org."Name", month;'''),
    ("059", "medium", "Số khách hàng duy nhất đã mua vé mỗi sự kiện", ["event", "payment"],
     '''SELECT e.name, COUNT(DISTINCT o.user_id) AS buyers FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN payment_service.orders o ON o.session_id = s.id WHERE o.status = 'Paid' GROUP BY e.id, e.name ORDER BY buyers DESC;'''),
    ("060", "medium", "Giá vé cao nhất của mỗi sự kiện", ["event"],
     '''SELECT e.name, MAX(l.price) AS max_price FROM event_service.events e JOIN event_service.legends l ON l.event_id = e.id GROUP BY e.id, e.name ORDER BY max_price DESC;'''),
    ("061", "medium", "Số ghế VIP của mỗi sự kiện", ["event", "seat"],
     '''SELECT e.name, COUNT(st.id) AS vip_seats FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN seat_service.seat_maps sm ON sm.session_id = s.id JOIN seat_service.seats st ON st.seat_map_id = sm.id JOIN event_service.legends l ON l.id = st.legend_id WHERE l.name = 'VIP' GROUP BY e.id, e.name ORDER BY vip_seats DESC;'''),
    ("062", "medium", "Tỉ lệ đơn đã thanh toán trên tổng đơn theo tổ chức", ["payment"],
     '''SELECT org_id, COUNT(*) FILTER (WHERE status = 'Paid') * 1.0 / COUNT(*) AS paid_ratio FROM payment_service.orders GROUP BY org_id ORDER BY paid_ratio DESC;'''),
    ("063", "medium", "Số thành viên là designer của mỗi tổ chức", ["org"],
     '''SELECT org."Name", COUNT(m."Id") AS designers FROM org_service."Organizations" org JOIN org_service."Permissions" p ON p."OrganizationId" = org."Id" AND p."IsDesigner" JOIN org_service."Members" m ON m."PermissionId" = p."Id" GROUP BY org."Id", org."Name" ORDER BY designers DESC;'''),

    # ───────────────────── HARD (subquery / window / HAVING / CASE) ─────────────────────
    ("064", "hard", "Top 3 loại vé doanh thu cao nhất của mỗi sự kiện", ["event", "payment"],
     '''SELECT * FROM (SELECT e.name AS event_name, oi.legend_name, SUM(oi.price) AS revenue, RANK() OVER (PARTITION BY e.id ORDER BY SUM(oi.price) DESC) AS rk FROM payment_service.order_items oi JOIN payment_service.orders o ON o.id = oi.order_id AND o.status = 'Paid' JOIN event_service.sessions s ON s.id = o.session_id JOIN event_service.events e ON e.id = s.event_id GROUP BY e.id, e.name, oi.legend_name) r WHERE rk <= 3 ORDER BY event_name, rk;'''),
    ("065", "hard", "Sự kiện có doanh thu vượt mức trung bình", ["event", "payment"],
     '''SELECT e.name, SUM(o.total_amount) AS revenue FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN payment_service.orders o ON o.session_id = s.id WHERE o.status = 'Paid' GROUP BY e.id, e.name HAVING SUM(o.total_amount) > (SELECT AVG(ev_rev) FROM (SELECT SUM(o2.total_amount) AS ev_rev FROM event_service.sessions s2 JOIN payment_service.orders o2 ON o2.session_id = s2.id WHERE o2.status = 'Paid' GROUP BY s2.event_id) x) ORDER BY revenue DESC;'''),
    ("066", "hard", "Khách hàng quay lại mua nhiều lần", ["user", "payment"],
     '''SELECT u."Username", COUNT(o.id) AS orders FROM user_service."Users" u JOIN payment_service.orders o ON o.user_id = u."Id" WHERE o.status = 'Paid' GROUP BY u."Id", u."Username" HAVING COUNT(o.id) > 1 ORDER BY orders DESC;'''),
    ("067", "hard", "Tỉ lệ check-in của mỗi session", ["event"],
     '''SELECT s.name, CASE WHEN COUNT(t.id) = 0 THEN 0 ELSE COUNT(*) FILTER (WHERE t.is_checked_in) * 1.0 / COUNT(t.id) END AS checkin_ratio FROM event_service.sessions s JOIN event_service.tickets t ON t.session_id = s.id GROUP BY s.id, s.name ORDER BY checkin_ratio DESC;'''),
    ("068", "hard", "So sánh doanh thu tháng này với tháng trước theo tổ chức", ["org", "payment"],
     '''SELECT o.org_id, SUM(o.total_amount) FILTER (WHERE DATE_TRUNC('month', o.paid_at) = DATE_TRUNC('month', CURRENT_DATE)) AS this_month, SUM(o.total_amount) FILTER (WHERE DATE_TRUNC('month', o.paid_at) = DATE_TRUNC('month', CURRENT_DATE - INTERVAL '1 month')) AS last_month FROM payment_service.orders o WHERE o.status = 'Paid' GROUP BY o.org_id ORDER BY this_month DESC NULLS LAST;'''),
    ("069", "hard", "Top 5 tổ chức theo doanh thu kèm phần trăm đóng góp", ["org", "payment"],
     '''SELECT org."Name", SUM(o.total_amount) AS revenue, SUM(o.total_amount) * 100.0 / SUM(SUM(o.total_amount)) OVER () AS pct FROM org_service."Organizations" org JOIN payment_service.orders o ON o.org_id = org."Id" WHERE o.status = 'Paid' GROUP BY org."Id", org."Name" ORDER BY revenue DESC LIMIT 5;'''),
    ("070", "hard", "Sự kiện chưa bán được vé nào", ["event"],
     '''SELECT e.id, e.name FROM event_service.events e WHERE NOT EXISTS (SELECT 1 FROM event_service.sessions s JOIN event_service.tickets t ON t.session_id = s.id WHERE s.event_id = e.id);'''),
    ("071", "hard", "Khách hàng chi tiêu trên mức trung bình", ["user", "payment"],
     '''SELECT u."Username", SUM(o.total_amount) AS spend FROM user_service."Users" u JOIN payment_service.orders o ON o.user_id = u."Id" WHERE o.status = 'Paid' GROUP BY u."Id", u."Username" HAVING SUM(o.total_amount) > (SELECT AVG(s) FROM (SELECT SUM(total_amount) AS s FROM payment_service.orders WHERE status = 'Paid' GROUP BY user_id) t) ORDER BY spend DESC;'''),
    ("072", "hard", "Xếp hạng sự kiện theo vé bán trong từng tổ chức", ["org", "event"],
     '''SELECT * FROM (SELECT e.organization_id, e.name, COUNT(t.id) AS sold, RANK() OVER (PARTITION BY e.organization_id ORDER BY COUNT(t.id) DESC) AS rk FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN event_service.tickets t ON t.session_id = s.id GROUP BY e.organization_id, e.id, e.name) r WHERE rk = 1 ORDER BY sold DESC;'''),
    ("073", "hard", "Sự kiện có tỉ lệ lấp đầy ghế trên 20%", ["event", "seat"],
     '''SELECT e.name, COUNT(*) FILTER (WHERE st.status = 'Sold') * 1.0 / COUNT(*) AS fill_rate FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN seat_service.seat_maps sm ON sm.session_id = s.id JOIN seat_service.seats st ON st.seat_map_id = sm.id GROUP BY e.id, e.name HAVING COUNT(*) FILTER (WHERE st.status = 'Sold') * 1.0 / COUNT(*) > 0.2 ORDER BY fill_rate DESC;'''),
    ("074", "hard", "Doanh thu lũy kế theo tháng", ["payment"],
     '''SELECT m, rev, SUM(rev) OVER (ORDER BY m) AS cumulative FROM (SELECT DATE_TRUNC('month', paid_at) AS m, SUM(total_amount) AS rev FROM payment_service.orders WHERE status = 'Paid' GROUP BY 1) t ORDER BY m;'''),
    ("075", "hard", "Sự kiện có hơn 3 vé đã check-in", ["event"],
     '''SELECT e.name, COUNT(*) FILTER (WHERE t.is_checked_in) AS checked_in FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN event_service.tickets t ON t.session_id = s.id GROUP BY e.id, e.name HAVING COUNT(*) FILTER (WHERE t.is_checked_in) > 3 ORDER BY checked_in DESC;'''),
    ("076", "hard", "Loại vé phổ biến nhất trong từng tổ chức", ["org", "seat", "event"],
     '''SELECT * FROM (SELECT sm.organization_id, l.name, COUNT(st.id) AS n, RANK() OVER (PARTITION BY sm.organization_id ORDER BY COUNT(st.id) DESC) AS rk FROM seat_service.seat_maps sm JOIN seat_service.seats st ON st.seat_map_id = sm.id JOIN event_service.legends l ON l.id = st.legend_id GROUP BY sm.organization_id, l.name) r WHERE rk = 1 ORDER BY n DESC;'''),

    # ───────────────────── CROSS-SERVICE (4-5 services) ─────────────────────
    ("077", "cross-service", "Báo cáo doanh thu và phí theo tổ chức kèm số ghế đã bán", ["org", "event", "seat", "payment"],
     '''SELECT org."Name", SUM(o.total_amount) AS revenue, SUM(o.platform_fee) AS fees, (SELECT COUNT(*) FROM seat_service.seat_maps sm JOIN seat_service.seats st ON st.seat_map_id = sm.id WHERE sm.organization_id = org."Id" AND st.status = 'Sold') AS sold_seats FROM org_service."Organizations" org JOIN payment_service.orders o ON o.org_id = org."Id" WHERE o.status = 'Paid' GROUP BY org."Id", org."Name" ORDER BY revenue DESC;'''),
    ("078", "cross-service", "Khách hàng mua vé ở nhiều sự kiện nhất", ["user", "payment", "event"],
     '''SELECT u."Username", COUNT(DISTINCT s.event_id) AS events FROM user_service."Users" u JOIN payment_service.orders o ON o.user_id = u."Id" JOIN event_service.sessions s ON s.id = o.session_id WHERE o.status = 'Paid' GROUP BY u."Id", u."Username" ORDER BY events DESC LIMIT 10;'''),
    ("079", "cross-service", "Top khách hàng mua ghế VIP nhiều nhất", ["user", "payment", "seat", "event"],
     '''SELECT u."Username", COUNT(*) AS vip_tickets FROM user_service."Users" u JOIN payment_service.orders o ON o.user_id = u."Id" JOIN payment_service.order_items oi ON oi.order_id = o.id JOIN seat_service.seats st ON st.id = oi.seat_id JOIN event_service.legends l ON l.id = st.legend_id WHERE o.status = 'Paid' AND l.name = 'VIP' GROUP BY u."Id", u."Username" ORDER BY vip_tickets DESC LIMIT 10;'''),
    ("080", "cross-service", "Doanh thu mỗi tổ chức kèm số sự kiện và số vé bán", ["org", "event", "payment"],
     '''SELECT org."Name", COUNT(DISTINCT e.id) AS events, COUNT(t.id) AS tickets, COALESCE(SUM(o.total_amount), 0) AS revenue FROM org_service."Organizations" org JOIN event_service.events e ON e.organization_id = org."Id" JOIN event_service.sessions s ON s.event_id = e.id LEFT JOIN event_service.tickets t ON t.session_id = s.id LEFT JOIN payment_service.orders o ON o.id = t.order_id AND o.status = 'Paid' GROUP BY org."Id", org."Name" ORDER BY revenue DESC;'''),
    ("081", "cross-service", "Tổ chức mà mỗi khách hàng chi tiêu nhiều nhất", ["user", "org", "payment"],
     '''SELECT * FROM (SELECT u."Username", org."Name" AS org_name, SUM(o.total_amount) AS spend, RANK() OVER (PARTITION BY u."Id" ORDER BY SUM(o.total_amount) DESC) AS rk FROM user_service."Users" u JOIN payment_service.orders o ON o.user_id = u."Id" JOIN org_service."Organizations" org ON org."Id" = o.org_id WHERE o.status = 'Paid' GROUP BY u."Id", u."Username", org."Id", org."Name") r WHERE rk = 1 ORDER BY spend DESC LIMIT 20;'''),
    ("082", "cross-service", "Top 10 sự kiện theo doanh thu kèm tên tổ chức và số ghế đã bán", ["event", "org", "seat", "payment"],
     '''SELECT e.name AS event_name, org."Name" AS org_name, SUM(o.total_amount) AS revenue, (SELECT COUNT(*) FROM seat_service.seat_maps sm JOIN seat_service.seats st ON st.seat_map_id = sm.id WHERE sm.event_id = e.id AND st.status = 'Sold') AS sold_seats FROM event_service.events e JOIN org_service."Organizations" org ON org."Id" = e.organization_id JOIN event_service.sessions s ON s.event_id = e.id JOIN payment_service.orders o ON o.session_id = s.id AND o.status = 'Paid' GROUP BY e.id, e.name, org."Name" ORDER BY revenue DESC LIMIT 10;'''),
    ("083", "cross-service", "Toàn cảnh mỗi tổ chức: số sự kiện, số vé bán, doanh thu", ["org", "event", "payment"],
     '''SELECT org."Name", (SELECT COUNT(*) FROM event_service.events e WHERE e.organization_id = org."Id") AS events, (SELECT COUNT(t.id) FROM event_service.events e JOIN event_service.sessions s ON s.event_id = e.id JOIN event_service.tickets t ON t.session_id = s.id WHERE e.organization_id = org."Id") AS tickets, COALESCE((SELECT SUM(o.total_amount) FROM payment_service.orders o WHERE o.org_id = org."Id" AND o.status = 'Paid'), 0) AS revenue FROM org_service."Organizations" org ORDER BY revenue DESC;'''),
]


def main() -> int:
    data = json.loads(DATASET.read_text(encoding="utf-8"))
    existing = {item["id"] for item in data}
    added = 0
    for qid, diff, q, svcs, sql in NEW:
        if qid in existing:
            continue
        data.append({"id": qid, "difficulty": diff, "question": q,
                     "involves_services": svcs, "gold_sql": sql, "verified": False})
        added += 1
    data.sort(key=lambda x: x["id"])
    DATASET.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Added {added} entries. Total now {len(data)}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
