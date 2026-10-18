-- Leaves only these roles in AspNetRoles: Administrator + the ten job roles.
-- The old menu-area roles (Warehouses, Products, ...) are no longer stored; a job role grants them at sign-in.
-- Run in SSMS against the WHMS database. Read the preview first. Take a backup before running.
BEGIN TRAN;

DECLARE @keep TABLE (Name nvarchar(256));
INSERT INTO @keep VALUES (N'Administrator'),(N'Warehouse Operator'),(N'Inventory Controller'),
                         (N'Buyer / Purchasing'),(N'Sales'),(N'Finance / Accountant'),(N'Manager'),
                         (N'Warehouse Supervisor'),(N'Quality / Returns Inspector'),(N'Customer Service'),(N'Auditor / Read-only');

-- 1. make sure the eleven roles exist
INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT LOWER(CONVERT(nvarchar(36), NEWID())), k.Name, UPPER(k.Name), LOWER(CONVERT(nvarchar(36), NEWID()))
FROM @keep k WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles r WHERE r.NormalizedName = UPPER(k.Name));

-- 2. everyone who held the 'Users' role (administrators) becomes Administrator
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT DISTINCT ur.UserId, adm.Id
FROM AspNetUserRoles ur
JOIN AspNetRoles old ON old.Id = ur.RoleId AND old.NormalizedName = N'USERS'
JOIN AspNetRoles adm ON adm.NormalizedName = N'ADMINISTRATOR'
WHERE NOT EXISTS (SELECT 1 FROM AspNetUserRoles x WHERE x.UserId = ur.UserId AND x.RoleId = adm.Id);

-- 3. the default admin is always Administrator
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT u.Id, adm.Id FROM AspNetUsers u, AspNetRoles adm
WHERE u.NormalizedEmail = N'ADMIN@ROOT.COM' AND adm.NormalizedName = N'ADMINISTRATOR'
  AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles x WHERE x.UserId = u.Id AND x.RoleId = adm.Id);

-- 4. PREVIEW: users (other than administrators) who lose menu access and need a job role afterwards
SELECT DISTINCT u.Email AS [Assign a job role to this user afterwards]
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON ur.UserId = u.Id
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE r.Name NOT IN (SELECT Name FROM @keep) AND r.NormalizedName NOT IN (N'PROFILES')
  AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles a JOIN AspNetRoles ar ON ar.Id = a.RoleId
                  WHERE a.UserId = u.Id AND ar.NormalizedName = N'ADMINISTRATOR');

-- 5. remove the old roles and their links
DELETE ur FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name NOT IN (SELECT Name FROM @keep);
DELETE FROM AspNetRoleClaims WHERE RoleId IN (SELECT Id FROM AspNetRoles WHERE Name NOT IN (SELECT Name FROM @keep));
DELETE FROM AspNetRoles WHERE Name NOT IN (SELECT Name FROM @keep);

SELECT Name FROM AspNetRoles ORDER BY Name;

COMMIT TRAN;   -- change to ROLLBACK TRAN if the preview or the final list looks wrong
