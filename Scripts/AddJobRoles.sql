-- Adds the ten job roles to AspNetRoles. Safe to run more than once.
-- (The application also does this by itself at startup; run this only if you want the rows now.)
DECLARE @roles TABLE (Name nvarchar(256));
INSERT INTO @roles (Name) VALUES
 (N'Warehouse Operator'),
 (N'Inventory Controller'),
 (N'Buyer / Purchasing'),
 (N'Sales'),
 (N'Finance / Accountant'),
 (N'Manager'),
 (N'Warehouse Supervisor'),
 (N'Quality / Returns Inspector'),
 (N'Customer Service'),
 (N'Auditor / Read-only');

INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), r.Name, UPPER(r.Name), LOWER(CONVERT(nvarchar(36), NEWID()))
FROM @roles r
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles x WHERE x.NormalizedName = UPPER(r.Name));

-- Optional: give an existing user a job role (edit the e-mail and role name, then run).
-- INSERT INTO AspNetUserRoles (UserId, RoleId)
-- SELECT u.Id, r.Id FROM AspNetUsers u, AspNetRoles r
-- WHERE u.NormalizedEmail = UPPER(N'someone@example.com') AND r.NormalizedName = UPPER(N'Warehouse Operator')
--   AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);

SELECT Name FROM AspNetRoles ORDER BY Name;
