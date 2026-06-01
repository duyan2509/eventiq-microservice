-- =============================================================
-- Prod-only: setup postgres_fdw on central analytics_db
-- Schema-prefix agnostic — target schemas keep same name as
-- source (user_service, org_service, ...) so SQL works in both
-- dev (Neon native) and prod (FDW) without rewriting.
-- =============================================================

CREATE EXTENSION IF NOT EXISTS postgres_fdw;

-- ---------- UserService ----------
CREATE SERVER IF NOT EXISTS user_server
  FOREIGN DATA WRAPPER postgres_fdw
  OPTIONS (host :'user_host', port '5432', dbname :'user_db');

CREATE USER MAPPING IF NOT EXISTS FOR current_user
  SERVER user_server
  OPTIONS (user :'fdw_user_name', password :'fdw_user_pwd');

CREATE SCHEMA IF NOT EXISTS user_service;
IMPORT FOREIGN SCHEMA "user_service"
  FROM SERVER user_server INTO user_service;

-- ---------- OrgService ----------
CREATE SERVER IF NOT EXISTS org_server
  FOREIGN DATA WRAPPER postgres_fdw
  OPTIONS (host :'org_host', port '5432', dbname :'org_db');

CREATE USER MAPPING IF NOT EXISTS FOR current_user
  SERVER org_server
  OPTIONS (user :'fdw_org_name', password :'fdw_org_pwd');

CREATE SCHEMA IF NOT EXISTS org_service;
IMPORT FOREIGN SCHEMA "org_service"
  FROM SERVER org_server INTO org_service;

-- ---------- EventService ----------
CREATE SERVER IF NOT EXISTS event_server
  FOREIGN DATA WRAPPER postgres_fdw
  OPTIONS (host :'event_host', port '5432', dbname :'event_db');

CREATE USER MAPPING IF NOT EXISTS FOR current_user
  SERVER event_server
  OPTIONS (user :'fdw_event_name', password :'fdw_event_pwd');

CREATE SCHEMA IF NOT EXISTS event_service;
IMPORT FOREIGN SCHEMA "event_service"
  FROM SERVER event_server INTO event_service;

-- ---------- SeatService ----------
CREATE SERVER IF NOT EXISTS seat_server
  FOREIGN DATA WRAPPER postgres_fdw
  OPTIONS (host :'seat_host', port '5432', dbname :'seat_db');

CREATE USER MAPPING IF NOT EXISTS FOR current_user
  SERVER seat_server
  OPTIONS (user :'fdw_seat_name', password :'fdw_seat_pwd');

CREATE SCHEMA IF NOT EXISTS seat_service;
IMPORT FOREIGN SCHEMA "seat_service"
  FROM SERVER seat_server INTO seat_service;

-- ---------- PaymentService ----------
CREATE SERVER IF NOT EXISTS payment_server
  FOREIGN DATA WRAPPER postgres_fdw
  OPTIONS (host :'payment_host', port '5432', dbname :'payment_db');

CREATE USER MAPPING IF NOT EXISTS FOR current_user
  SERVER payment_server
  OPTIONS (user :'fdw_payment_name', password :'fdw_payment_pwd');

CREATE SCHEMA IF NOT EXISTS payment_service;
IMPORT FOREIGN SCHEMA "payment_service"
  FROM SERVER payment_server INTO payment_service;

-- ---------- Verification ----------
SELECT table_schema, COUNT(*) AS table_count
FROM information_schema.tables
WHERE table_schema IN
      ('user_service','org_service','event_service','seat_service','payment_service')
GROUP BY 1
ORDER BY 1;
