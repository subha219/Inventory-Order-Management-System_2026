/* =====================================================================================
   Inventra - clear all business data for a NEW CLIENT set-up
   Run in SSMS against the application database (default name: WHMS).

   KEEPS   : the database structure, __EFMigrationsHistory, roles and role claims,
             the default admin login (and optionally every other user, see @RemoveOtherUsers),
             Company (your company details), number-sequence settings (counters go back to 0),
             permission rules, and the six system warehouses (Customer, Vendor, Transfer,
             Adjustment, StockCount, Scrapping).
   DELETES : customers, vendors, products, stock, orders, deliveries, receipts, returns,
             transfers, adjustments, counts, warehouses and bins you created, tasks, holds,
             shipments, kits, waves, documents, audit log, notifications, automation rules,
             scheduled reports, integrations, portal links, taxes, units of measure and more.

   BEFORE YOU RUN:  1) take a BACKUP.   2) stop the application.
   AFTER YOU RUN :  3) in appsettings.Development.json set "IsDemoVersion": false (when it is true,
                       demo customers/products are created again on start-up).
                    4) in appsettings.json set "CreateDemoLogins": false for the client copy.
                    5) start the app, sign in as admin, fill in Company, Taxes, Units, Warehouses.
                    6) delete any uploaded files left on disk under wwwroot (app_data/uploads), if used.
   The whole script runs in one transaction. The last line is ROLLBACK so a first run changes
   nothing: read the PREVIEW, then change it to COMMIT and run again.
   ===================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RemoveOtherUsers bit = 1;                    -- 1 = remove every user except the default admin (and their role links)
DECLARE @AdminEmail nvarchar(256) = N'admin@root.com'; -- the login that is kept

BEGIN TRAN;

-- Tables that are never emptied
DECLARE @keep TABLE (name sysname PRIMARY KEY);
INSERT INTO @keep (name) VALUES
 (N'__EFMigrationsHistory'),
 (N'AspNetRoles'), (N'AspNetRoleClaims'),
 (N'AspNetUsers'), (N'AspNetUserRoles'), (N'AspNetUserClaims'), (N'AspNetUserLogins'), (N'AspNetUserTokens'),
 (N'Company'), (N'NumberSequence'), (N'PermissionRule'), (N'Warehouse');   -- Warehouse and NumberSequence get special handling below

-- 1. PREVIEW: how many rows will go
CREATE TABLE #preview (TableName sysname, RowsToDelete bigint);
DECLARE @t sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.name FROM sys.tables t WHERE t.is_ms_shipped = 0 AND t.name NOT IN (SELECT name FROM @keep) ORDER BY t.name;
OPEN c; FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'INSERT INTO #preview SELECT N''' + @t + N''', COUNT_BIG(*) FROM ' + QUOTENAME(@t);
    EXEC (@sql);
    FETCH NEXT FROM c INTO @t;
END
CLOSE c; DEALLOCATE c;
INSERT INTO #preview SELECT N'Warehouse (non-system)', COUNT_BIG(*) FROM Warehouse WHERE ISNULL(SystemWarehouse, 0) = 0;
IF @RemoveOtherUsers = 1
    INSERT INTO #preview SELECT N'AspNetUsers (other than admin)', COUNT_BIG(*) FROM AspNetUsers WHERE NormalizedEmail <> UPPER(@AdminEmail) OR NormalizedEmail IS NULL;
SELECT TableName, RowsToDelete FROM #preview WHERE RowsToDelete > 0 ORDER BY RowsToDelete DESC;

-- 2. Switch off foreign keys while deleting
DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.tables WHERE is_ms_shipped = 0;
OPEN c2; FETCH NEXT FROM c2 INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE ' + QUOTENAME(@t) + N' NOCHECK CONSTRAINT ALL';
    EXEC (@sql);
    FETCH NEXT FROM c2 INTO @t;
END
CLOSE c2; DEALLOCATE c2;

-- 3. Empty the business tables
DECLARE c3 CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.tables WHERE is_ms_shipped = 0 AND name NOT IN (SELECT name FROM @keep);
OPEN c3; FETCH NEXT FROM c3 INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DELETE FROM ' + QUOTENAME(@t);
    EXEC (@sql);
    FETCH NEXT FROM c3 INTO @t;
END
CLOSE c3; DEALLOCATE c3;

-- Warehouses: keep only the system ones
DELETE FROM Warehouse WHERE ISNULL(SystemWarehouse, 0) = 0;

-- Document numbers start again from 1 (prefixes and suffixes are kept)
UPDATE NumberSequence SET LastUsedCount = 0;

-- Users
IF @RemoveOtherUsers = 1
BEGIN
    DECLARE @gone TABLE (Id nvarchar(450) PRIMARY KEY);
    INSERT INTO @gone SELECT Id FROM AspNetUsers WHERE NormalizedEmail <> UPPER(@AdminEmail) OR NormalizedEmail IS NULL;
    DELETE FROM AspNetUserRoles   WHERE UserId IN (SELECT Id FROM @gone);
    DELETE FROM AspNetUserClaims  WHERE UserId IN (SELECT Id FROM @gone);
    DELETE FROM AspNetUserLogins  WHERE UserId IN (SELECT Id FROM @gone);
    DELETE FROM AspNetUserTokens  WHERE UserId IN (SELECT Id FROM @gone);
    DELETE FROM AspNetUsers       WHERE Id IN (SELECT Id FROM @gone);
END

-- 4. Switch foreign keys back on and verify them
DECLARE c4 CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.tables WHERE is_ms_shipped = 0;
OPEN c4; FETCH NEXT FROM c4 INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE ' + QUOTENAME(@t) + N' WITH CHECK CHECK CONSTRAINT ALL';
    EXEC (@sql);
    FETCH NEXT FROM c4 INTO @t;
END
CLOSE c4; DEALLOCATE c4;

-- 5. Restart identity counters (only tables that have one)
DECLARE c5 CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.tables WHERE is_ms_shipped = 0 AND OBJECTPROPERTY(object_id, 'TableHasIdentity') = 1 AND name NOT LIKE 'AspNet%' AND name <> '__EFMigrationsHistory';
OPEN c5; FETCH NEXT FROM c5 INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DBCC CHECKIDENT (' + QUOTENAME(@t, '''') + N', RESEED, 0) WITH NO_INFOMSGS';
    EXEC (@sql);
    FETCH NEXT FROM c5 INTO @t;
END
CLOSE c5; DEALLOCATE c5;

-- 6. What is left
SELECT N'AspNetUsers' AS Item, COUNT(*) AS Rows FROM AspNetUsers
UNION ALL SELECT N'AspNetRoles', COUNT(*) FROM AspNetRoles
UNION ALL SELECT N'Warehouse (system)', COUNT(*) FROM Warehouse
UNION ALL SELECT N'Company', COUNT(*) FROM Company
UNION ALL SELECT N'Product', COUNT(*) FROM Product
UNION ALL SELECT N'SalesOrder', COUNT(*) FROM SalesOrder
UNION ALL SELECT N'PurchaseOrder', COUNT(*) FROM PurchaseOrder;

DROP TABLE #preview;

ROLLBACK TRAN;   -- first run: nothing is changed. After reading the preview, change this line to COMMIT TRAN and run again.
