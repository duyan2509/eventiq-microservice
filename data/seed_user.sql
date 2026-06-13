\set load_test_org_id 'a0000000-0000-0000-0000-000000000001'

INSERT INTO user_service."UserRoles"
  ("Id", "UserId", "RoleId", "OrganizationId", "CreatedAt", "UpdatedAt", "IsDeleted")
SELECT
  gen_random_uuid(), :'load_test_user_id'::uuid, r."Id",
  :'load_test_org_id'::uuid, NOW(), NOW(), false
FROM user_service."Roles" r
WHERE r."Name" = 'Organization'
LIMIT 1
ON CONFLICT DO NOTHING;

SELECT 'user_roles' AS tbl, COUNT(*) AS n
FROM user_service."UserRoles"
WHERE "UserId" = :'load_test_user_id'::uuid;
