from __future__ import annotations

import argparse
import random
import sys
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path

from faker import Faker
from psycopg2.extras import Json, execute_values, register_uuid

register_uuid()

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from src.db import connect, current_mode  # noqa: E402

fake = Faker("vi_VN")
Faker.seed(20242025)
random.seed(20242025)
NOW = datetime.now(timezone.utc)

LEGEND_CATALOG = [("VIP", 2_000_000, 2), ("Premium", 1_000_000, 1),
                  ("Standard", 500_000, 3), ("Economy", 200_000, 4)]
SEAT_STATUS_WEIGHTS = (["Sold"] * 35 + ["Available"] * 60 + ["Holding"] * 3 + ["Blocked"] * 2)
EVENT_STATUS_DIST = {0: 5, 1: 8, 2: 20, 3: 5, 4: 37, 5: 5}

BUSINESS_TABLES = [
    'event_service.tickets', 'payment_service.order_items', 'payment_service.orders',
    'seat_service.versions', 'seat_service.objects', 'seat_service.seats',
    'seat_service.seat_maps', 'event_service.submissions', 'event_service.sessions',
    'event_service.legends', 'event_service.charts', 'event_service.org_payment_info',
    'event_service.events', 'org_service."PayoutLogs"', 'org_service."Invitations"',
    'org_service."Members"', 'org_service."Permissions"', 'org_service."PlatformConfigs"',
    'org_service."Organizations"', 'user_service."PasswordResetTokens"',
    'user_service."RefreshTokens"', 'user_service."BanHistories"',
    'user_service."UserRoles"', 'user_service."Users"', 'user_service."Roles"',
]


def insert(cur, table, cols, rows, page_size=1000):
    if not rows:
        return
    collist = ", ".join(cols)
    execute_values(cur, f"INSERT INTO {table} ({collist}) VALUES %s", rows, page_size=page_size)


def rand_dt(days_back_min, days_back_max):
    return NOW - timedelta(days=random.randint(days_back_min, days_back_max),
                           hours=random.randint(0, 23), minutes=random.randint(0, 59))


def seed(cur):
    for tbl in BUSINESS_TABLES:
        cur.execute(f"TRUNCATE TABLE {tbl} CASCADE;")

    # ---- Roles ----
    roles = {name: uuid.uuid4() for name in ["Admin", "User", "Staff", "Organization"]}
    insert(cur, 'user_service."Roles"', ['"Id"', '"Name"', '"CreatedAt"', '"IsDeleted"'],
           [(rid, name, NOW, False) for name, rid in roles.items()])

    # ---- Users ----
    users = []
    for i in range(200):
        users.append((uuid.uuid4(), f"user{i}_{fake.user_name()}@example.com",
                      f"{fake.user_name()}{i}", False,
                      "$2a$11$" + uuid.uuid4().hex + uuid.uuid4().hex[:21], "",
                      rand_dt(0, 180), None, None, False))
    insert(cur, 'user_service."Users"',
           ['"Id"', '"Email"', '"Username"', '"IsBanned"', '"PasswordHash"', '"Avatar"',
            '"CreatedAt"', '"UpdatedAt"', '"DeletedAt"', '"IsDeleted"'], users)
    uid = [u[0] for u in users]
    uname = {u[0]: u[2] for u in users}
    uemail = {u[0]: u[1] for u in users}

    org_owners, customers, staffs = uid[:30], uid[30:200], uid[180:190]
    user_roles = ([(uuid.uuid4(), o, roles["Organization"], None, NOW, False) for o in org_owners]
                  + [(uuid.uuid4(), c, roles["User"], None, NOW, False) for c in customers]
                  + [(uuid.uuid4(), s, roles["Staff"], None, NOW, False) for s in staffs])
    insert(cur, 'user_service."UserRoles"',
           ['"Id"', '"UserId"', '"RoleId"', '"OrganizationId"', '"CreatedAt"', '"IsDeleted"'],
           user_roles)

    banned = random.sample(uid, 10)
    insert(cur, 'user_service."BanHistories"',
           ['"Id"', '"UserId"', '"Reason"', '"BannedById"', '"CreatedAt"', '"IsDeleted"'],
           [(uuid.uuid4(), b, fake.sentence(), roles["Admin"] and random.choice(org_owners),
             rand_dt(0, 90), False) for b in banned])
    insert(cur, 'user_service."RefreshTokens"',
           ['"Id"', '"Token"', '"Expires"', '"UserId"', '"CreatedAt"', '"IsDeleted"'],
           [(uuid.uuid4(), uuid.uuid4().hex, NOW + timedelta(days=7), random.choice(uid),
             NOW, False) for _ in range(100)])
    insert(cur, 'user_service."PasswordResetTokens"',
           ['"Id"', '"Token"', '"Expires"', '"UserId"', '"CreatedAt"', '"IsDeleted"'],
           [(uuid.uuid4(), uuid.uuid4().hex, NOW + timedelta(days=1), u, NOW, False)
            for u in random.sample(uid, 20)])

    # ---- Organizations ----
    orgs = []
    for i, owner in enumerate(org_owners):
        active = i < 20
        orgs.append((uuid.uuid4(), fake.company(), fake.catch_phrase(), owner, uemail[owner],
                     f"acct_test_{uuid.uuid4().hex[:14]}" if active else None,
                     2 if active else 0,
                     rand_dt(30, 200) if active else None,
                     rand_dt(60, 300), None, None, False))
    insert(cur, 'org_service."Organizations"',
           ['"Id"', '"Name"', '"Description"', '"OwnerId"', '"OwnerEmail"', '"StripeAccountId"',
            '"PaymentStatus"', '"PaymentConfiguredAt"', '"CreatedAt"', '"UpdatedAt"',
            '"DeletedAt"', '"IsDeleted"'], orgs)
    org_ids = [o[0] for o in orgs]
    org_name = {o[0]: o[1] for o in orgs}
    org_active = {o[0]: (o[6] == 2) for o in orgs}

    # ---- Permissions (2/org), Members, Invitations, PayoutLogs ----
    perms, perm_by_org = [], {}
    for oid in org_ids:
        designer, viewer = uuid.uuid4(), uuid.uuid4()
        perm_by_org[oid] = [designer, viewer]
        perms += [(designer, "Designer", oid, True, NOW, None, None, False),
                  (viewer, "Viewer", oid, False, NOW, None, None, False)]
    insert(cur, 'org_service."Permissions"',
           ['"Id"', '"Name"', '"OrganizationId"', '"IsDesigner"', '"CreatedAt"', '"UpdatedAt"',
            '"DeletedAt"', '"IsDeleted"'], perms)

    members = []
    for oid in org_ids:
        for _ in range(random.randint(3, 5)):
            mu = random.choice(staffs + customers) if random.random() > 0.3 else None
            members.append((uuid.uuid4(), mu, uname.get(mu, fake.user_name()) + "@example.com",
                            oid, random.choice(perm_by_org[oid]), NOW, None, None, False))
    insert(cur, 'org_service."Members"',
           ['"Id"', '"UserId"', '"Email"', '"OrganizationId"', '"PermissionId"', '"CreatedAt"',
            '"UpdatedAt"', '"DeletedAt"', '"IsDeleted"'], members)

    invites = []
    for _ in range(40):
        oid = random.choice(org_ids)
        invites.append((uuid.uuid4(), oid, random.choice(uid + [None]),
                        fake.user_name() + "@example.com", NOW + timedelta(days=14),
                        random.randint(0, 3), random.choice(perm_by_org[oid]),
                        NOW, None, None, False))
    insert(cur, 'org_service."Invitations"',
           ['"Id"', '"OrganizationId"', '"UserId"', '"UserEmail"', '"ExpiresAt"', '"Status"',
            '"PermissionId"', '"CreatedAt"', '"UpdatedAt"', '"DeletedAt"', '"IsDeleted"'], invites)

    insert(cur, 'org_service."PayoutLogs"',
           ['"Id"', '"OrgId"', '"StripePayoutId"', '"Amount"', '"Currency"', '"TriggeredAt"'],
           [(uuid.uuid4(), oid, f"po_{uuid.uuid4().hex[:12]}", random.randint(500_000, 50_000_000),
             "vnd", rand_dt(0, 180)) for oid in org_ids if org_active[oid]])

    insert(cur, 'org_service."PlatformConfigs"',
           ['"Id"', '"CurrentFeeRate"', '"PendingFeeRate"', '"EffectiveDate"',
            '"PayoutDayOfMonth"', '"UpdatedAt"', '"UpdatedBy"'],
           [(1, 0.05, 0.05, NOW - timedelta(days=120), 5, NOW, random.choice(org_owners))])

    # ---- Events + children ----
    status_pool = [s for s, n in EVENT_STATUS_DIST.items() for _ in range(n)]
    random.shuffle(status_pool)

    events, charts, legends, sessions, submissions, opi = [], [], [], [], [], []
    ev_status, ev_legends, sess_meta = {}, {}, []  # sess_meta: (sid, eid, chart_id, status)
    for status in status_pool:
        eid, oid = uuid.uuid4(), random.choice(org_ids)
        start = NOW + timedelta(days=random.randint(-120, 90))
        end = start + timedelta(days=random.randint(1, 5))
        events.append((eid, oid, org_name[oid], None, None, fake.catch_phrase()[:60],
                       fake.text(120), fake.street_address(), "79", "760", "Hồ Chí Minh",
                       "Quận 1", status, start, end, rand_dt(10, 150), None, None, False))
        ev_status[eid] = status

        chart_id = uuid.uuid4()
        charts.append((chart_id, f"{fake.word().title()} Chart", eid, NOW, None, None, False))

        catalog = [LEGEND_CATALOG[0]] + random.sample(LEGEND_CATALOG[1:], random.randint(1, 3))
        elg = []
        for name, price, stype in catalog:
            lid = uuid.uuid4()
            elg.append((lid, name, price, stype))
            legends.append((lid, name, fake.hex_color(), price, eid, NOW, None, None, False))
        ev_legends[eid] = elg

        n = random.randint(2, 3)
        dur = (end - start) / n
        for k in range(n):
            sid = uuid.uuid4()
            s_start = start + dur * k
            sessions.append((sid, f"Session {k + 1}", s_start, s_start + dur * 0.9, eid,
                             chart_id, NOW, None, None, False))
            sess_meta.append((sid, eid, chart_id, status))

        sub_status = {0: 0, 1: 0, 2: 1, 3: 2, 4: 1, 5: 3}[status]
        submissions.append((uuid.uuid4(), eid, uemail[random.choice(org_owners)],
                            random.choice(org_owners), fake.sentence(), sub_status,
                            NOW, None, None, False))
        if org_active[oid]:
            opi.append((oid, f"acct_test_{uuid.uuid4().hex[:14]}", True, NOW))

    insert(cur, 'event_service.events',
           ['id', 'organization_id', 'organization_name', 'oranization_avatar', 'event_banner',
            'name', 'description', 'detail_address', 'province_code', 'commune_code',
            'province_name', 'commune_name', 'status', 'start_time', 'end_time',
            'created_at', 'updated_at', 'deleted_at', 'is_deleted'], events)
    insert(cur, 'event_service.charts',
           ['id', 'name', 'event_id', 'created_at', 'updated_at', 'deleted_at', 'is_deleted'], charts)
    insert(cur, 'event_service.legends',
           ['id', 'name', 'color', 'price', 'event_id', 'created_at', 'updated_at',
            'deleted_at', 'is_deleted'], legends)
    insert(cur, 'event_service.sessions',
           ['id', 'name', 'start_time', 'end_time', 'event_id', 'chart_id', 'created_at',
            'updated_at', 'deleted_at', 'is_deleted'], sessions)
    insert(cur, 'event_service.submissions',
           ['id', 'event_id', 'admin_email', 'admin_id', 'message', 'status', 'created_at',
            'updated_at', 'deleted_at', 'is_deleted'], submissions)
    # org_payment_info: dedupe by org (PK = organization_id)
    seen = set()
    opi_u = [r for r in opi if not (r[0] in seen or seen.add(r[0]))]
    insert(cur, 'event_service.org_payment_info',
           ['organization_id', 'stripe_account_id', 'is_active', 'updated_at'], opi_u)

    # ---- SeatMaps + Seats + Objects + Versions (Approved/Published only) ----
    seat_maps, seats, objects, versions = [], [], [], []
    seatmap_seats = {}  # sm_id -> list of (seat_id, legend_name, price, status)
    for sid, eid, chart_id, status in sess_meta:
        if status not in (2, 4):
            continue
        oid = next(e[1] for e in events if e[0] == eid)
        sm_id = uuid.uuid4()
        seat_maps.append((sm_id, chart_id, eid, oid, f"Map {fake.word()}", "Published",
                          Json({"width": 1000, "height": 800}), 1, NOW, None, None, False,
                          sid, 100))
        elg = ev_legends[eid]
        pool = []
        for r in range(10):
            for c in range(10):
                lid, lname, lprice, stype = random.choice(elg)
                st = random.choice(SEAT_STATUS_WEIGHTS)
                seat_id = uuid.uuid4()
                seats.append((seat_id, f"{chr(65 + r)}{c + 1}", r * 10 + c + 1, st, stype,
                              Json({"x": c * 40, "y": r * 40}), lid, None, NOW, None, None,
                              False, None, None, sm_id))
                pool.append((seat_id, lname, lprice, st))
        seatmap_seats[sm_id] = pool
        for z, otype in enumerate(["stage", "exit", "entrance"]):
            objects.append((uuid.uuid4(), sm_id, otype, otype.title(),
                            Json({"x": 0, "y": 0, "w": 100, "h": 50}), Json({"fill": "#ccc"}),
                            z, NOW, None, None, False))
        versions.append((uuid.uuid4(), sm_id, 1, Json({}), random.choice(org_owners),
                         "initial", NOW, None, None, False))

    insert(cur, 'seat_service.seat_maps',
           ['id', 'chart_id', 'event_id', 'organization_id', 'name', 'status', 'canvas_settings',
            'version', 'created_at', 'updated_at', 'deleted_at', 'is_deleted', 'session_id',
            'total_seats'], seat_maps)
    insert(cur, 'seat_service.seats',
           ['id', 'label', 'seat_number', 'status', 'seat_type', 'position', 'legend_id',
            'custom_properties', 'created_at', 'updated_at', 'deleted_at', 'is_deleted',
            'held_by', 'held_until', 'seat_map_id'],
           seats, page_size=2000)
    insert(cur, 'seat_service.objects',
           ['id', 'seat_map_id', 'object_type', 'label', 'geometry', 'style', 'z_index',
            'created_at', 'updated_at', 'deleted_at', 'is_deleted'], objects)
    insert(cur, 'seat_service.versions',
           ['id', 'seat_map_id', 'version_number', 'snapshot', 'created_by', 'change_description',
            'created_at', 'updated_at', 'deleted_at', 'is_deleted'], versions)

    # ---- Orders + OrderItems + Tickets ----
    session_to_sm = {sm[12]: sm[0] for sm in seat_maps}  # session_id -> seat_map_id
    sm_to_event = {sm[0]: sm[2] for sm in seat_maps}
    event_to_org = {e[0]: e[1] for e in events}
    event_name = {e[0]: e[5] for e in events}
    sess_by_id = {s[0]: s for s in sessions}
    sellable_sessions = list(session_to_sm.keys())

    orders, order_items, tickets = [], [], []
    for _ in range(500):
        if not sellable_sessions:
            break
        sid = random.choice(sellable_sessions)
        sm_id = session_to_sm[sid]
        eid = sm_to_event[sm_id]
        oid = uuid.uuid4()
        status = random.choices(["Paid", "Pending", "Failed"], weights=[70, 20, 10])[0]
        buyer = random.choice(customers)
        sess = sess_by_id[sid]

        chosen = random.sample(seatmap_seats[sm_id], random.randint(2, 4))
        total = sum(p for _, _, p, _ in chosen)
        paid_at = rand_dt(0, 165) if status == "Paid" else None
        created = paid_at or rand_dt(0, 20)
        orders.append((oid, buyer, event_to_org[eid], sid, f"cs_test_{uuid.uuid4().hex[:14]}",
                       status, total, round(total * 0.05), event_name[eid], sess[1], sess[2],
                       paid_at, created, None, None, False,
                       "Webhook" if status == "Paid" else None))
        if status in ("Paid", "Pending"):
            for seat_id, lname, price, _ in chosen:
                oi_id = uuid.uuid4()
                order_items.append((oi_id, oid, seat_id, f"seat-{str(seat_id)[:4]}", lname,
                                    price, created, None, None, False))
                if status == "Paid":
                    tickets.append((uuid.uuid4(), oid, sid, seat_id, f"seat-{str(seat_id)[:4]}",
                                    lname, price, uuid.uuid4().hex, random.random() < 0.4,
                                    (paid_at + timedelta(days=1)) if random.random() < 0.4 else None,
                                    paid_at, NOW, None, None, False))

    insert(cur, 'payment_service.orders',
           ['id', 'user_id', 'org_id', 'session_id', 'stripe_session_id', 'status',
            'total_amount', 'platform_fee', 'event_name', 'session_name', 'session_date',
            'paid_at', 'created_at', 'updated_at', 'deleted_at', 'is_deleted', 'settled_by'],
           orders)
    insert(cur, 'payment_service.order_items',
           ['id', 'order_id', 'seat_id', 'seat_label', 'legend_name', 'price', 'created_at',
            'updated_at', 'deleted_at', 'is_deleted'], order_items)
    insert(cur, 'event_service.tickets',
           ['id', 'order_id', 'session_id', 'seat_id', 'seat_label', 'legend_name', 'price',
            'qr_code', 'is_checked_in', 'checked_in_at', 'issued_at', 'created_at', 'updated_at',
            'deleted_at', 'is_deleted'], tickets)

    return {"users": len(users), "orgs": len(orgs), "events": len(events),
            "sessions": len(sessions), "seat_maps": len(seat_maps), "seats": len(seats),
            "orders": len(orders), "order_items": len(order_items), "tickets": len(tickets)}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--i-understand-this-truncates", action="store_true")
    args = ap.parse_args()

    mode = current_mode()
    if mode != "eval" and not args.__dict__["i_understand_this_truncates"]:
        raise SystemExit(
            f"Refusing to seed: ANALYTICS_MODE={mode}. This script TRUNCATEs every "
            f"business table. Set ANALYTICS_MODE=eval (EVAL_DB_* in .env) so it only "
            f"touches the isolated eval branch, or pass --i-understand-this-truncates."
        )

    print(f"[seed] mode={mode} — seeding...")
    with connect() as conn:
        cur = conn.cursor()
        counts = seed(cur)
        conn.commit()
    print("[seed] committed:")
    for k, v in counts.items():
        print(f"  {k:<12s}: {v}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
