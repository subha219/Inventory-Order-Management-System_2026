using Application.Common.CQS.Commands;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Infrastructure.DataAccessManager.EFCore.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Infrastructure.DataAccessManager.EFCore;



public static class DI
{
    public static IServiceCollection RegisterDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var databaseProvider = configuration["DatabaseProvider"];

        // Sensitive data logging writes parameter values (incl. personal data) to logs.
        // It is OFF unless explicitly enabled (Database:EnableSensitiveDataLogging=true, e.g. in Development).
        var enableSensitiveLogging = configuration.GetValue<bool>("Database:EnableSensitiveDataLogging");
        void Configure(DbContextOptionsBuilder options)
        {
            options.AddInterceptors(new AuditSaveChangesInterceptor());
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 2, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: null));
            if (enableSensitiveLogging)
            {
                options.LogTo(Log.Information, LogLevel.Information).EnableSensitiveDataLogging();
            }
        }

        // Register Context
        switch (databaseProvider)
        {
            //case "MySql":
            //    services.AddDbContext<DataContext>(options =>
            //        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)))
            //        .LogTo(Log.Information, LogLevel.Information)
            //        .EnableSensitiveDataLogging()
            //    );
            //    services.AddDbContext<CommandContext>(options =>
            //        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)))
            //        .LogTo(Log.Information, LogLevel.Information)
            //        .EnableSensitiveDataLogging()
            //    );
            //    services.AddDbContext<QueryContext>(options =>
            //        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)))
            //        .LogTo(Log.Information, LogLevel.Information)
            //        .EnableSensitiveDataLogging()
            //    );
            //    break;

            case "SqlServer":
            default:
                services.AddDbContext<DataContext>(Configure);
                services.AddDbContext<CommandContext>(Configure);
                services.AddDbContext<QueryContext>(Configure);
                break;
        }


        services.AddScoped<ICommandContext, CommandContext>();
        services.AddScoped<IQueryContext, QueryContext>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(ICommandRepository<>), typeof(CommandRepository<>));


        return services;
    }

    public static IHost CreateDatabase(this IHost host)
    {
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var useMigrations = configuration.GetValue<bool>("Database:UseMigrations");

        using var scope = host.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        // Create database using DataContext
        var dataContext = serviceProvider.GetRequiredService<DataContext>();
        if (useMigrations)
        {
            // Production path: apply EF Core migrations (add them with `dotnet ef migrations add ...`).
            dataContext.Database.Migrate();
        }
        else
        {
            dataContext.Database.EnsureCreated(); // Quick start / development only

            // EnsureCreated never alters an existing database, so add newer tables idempotently.
            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ProductBatch', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductBatch (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        ProductId nvarchar(50) NULL,
        WarehouseId nvarchar(50) NULL,
        BatchNumber nvarchar(255) NULL,
        ManufactureDate datetime2 NULL,
        ExpiryDate datetime2 NULL,
        Quantity float NULL,
        Notes nvarchar(4000) NULL
    );
    CREATE INDEX IX_ProductBatch_ProductId ON dbo.ProductBatch(ProductId);
    CREATE INDEX IX_ProductBatch_ExpiryDate ON dbo.ProductBatch(ExpiryDate);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductBatch table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        UserId nvarchar(450) NULL,
        Action nvarchar(50) NULL,
        EntityName nvarchar(255) NULL,
        EntityId nvarchar(50) NULL,
        EntityLabel nvarchar(255) NULL,
        Changes nvarchar(4000) NULL
    );
    CREATE INDEX IX_AuditLog_CreatedAtUtc ON dbo.AuditLog(CreatedAtUtc);
    CREATE INDEX IX_AuditLog_EntityName ON dbo.AuditLog(EntityName);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("AuditLog table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.StorageLocation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StorageLocation (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        WarehouseId nvarchar(50) NULL,
        Code nvarchar(255) NULL,
        Zone nvarchar(255) NULL,
        Aisle nvarchar(255) NULL,
        Rack nvarchar(255) NULL,
        Shelf nvarchar(255) NULL,
        Bin nvarchar(255) NULL,
        LocationType nvarchar(50) NULL,
        Capacity float NULL,
        IsActive bit NOT NULL DEFAULT 1,
        Notes nvarchar(4000) NULL
    );
    CREATE INDEX IX_StorageLocation_WarehouseId ON dbo.StorageLocation(WarehouseId);
    CREATE INDEX IX_StorageLocation_Code ON dbo.StorageLocation(Code);
END
IF OBJECT_ID(N'dbo.LocationStock', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LocationStock (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        LocationId nvarchar(50) NULL,
        ProductId nvarchar(50) NULL,
        Quantity float NULL
    );
    CREATE INDEX IX_LocationStock_LocationId ON dbo.LocationStock(LocationId);
    CREATE INDEX IX_LocationStock_ProductId ON dbo.LocationStock(ProductId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("StorageLocation table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ApprovalRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalRequest (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        EntityType nvarchar(255) NULL,
        EntityId nvarchar(50) NULL,
        EntityNumber nvarchar(255) NULL,
        Units float NULL,
        Status nvarchar(50) NULL,
        RequestedById nvarchar(450) NULL,
        DecidedById nvarchar(450) NULL,
        DecidedAtUtc datetime2 NULL,
        DecisionNote nvarchar(4000) NULL
    );
    CREATE INDEX IX_ApprovalRequest_Status ON dbo.ApprovalRequest(Status);
    CREATE INDEX IX_ApprovalRequest_EntityId ON dbo.ApprovalRequest(EntityId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ApprovalRequest table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.DeliveryPick', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryPick (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        DeliveryOrderId nvarchar(50) NULL,
        ProductId nvarchar(50) NULL,
        LocationId nvarchar(50) NULL,
        Quantity float NULL
    );
    CREATE INDEX IX_DeliveryPick_DeliveryOrderId ON dbo.DeliveryPick(DeliveryOrderId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("DeliveryPick table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.QualityInspection', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QualityInspection (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        GoodsReceiveId nvarchar(50) NULL,
        ProductId nvarchar(50) NULL,
        PassedQuantity float NULL,
        FailedQuantity float NULL,
        Result nvarchar(50) NULL,
        Reason nvarchar(4000) NULL
    );
    CREATE INDEX IX_QualityInspection_GoodsReceiveId ON dbo.QualityInspection(GoodsReceiveId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("QualityInspection table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.PutAwayRecord', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PutAwayRecord (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        GoodsReceiveId nvarchar(50) NULL,
        ProductId nvarchar(50) NULL,
        LocationId nvarchar(50) NULL,
        Quantity float NULL
    );
    CREATE INDEX IX_PutAwayRecord_GoodsReceiveId ON dbo.PutAwayRecord(GoodsReceiveId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("PutAwayRecord table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ProductSerial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductSerial (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        ProductId nvarchar(50) NULL,
        SerialNumber nvarchar(255) NULL,
        Status nvarchar(50) NULL,
        WarehouseId nvarchar(50) NULL,
        LocationId nvarchar(50) NULL,
        GoodsReceiveId nvarchar(50) NULL,
        GoodsReceiveNumber nvarchar(255) NULL,
        DeliveryOrderId nvarchar(50) NULL,
        DeliveryOrderNumber nvarchar(255) NULL,
        CustomerName nvarchar(255) NULL,
        ShippedAtUtc datetime2 NULL,
        Notes nvarchar(4000) NULL
    );
    CREATE INDEX IX_ProductSerial_SerialNumber ON dbo.ProductSerial(SerialNumber);
    CREATE INDEX IX_ProductSerial_ProductId ON dbo.ProductSerial(ProductId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductSerial table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.DeliveryPickBatch', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryPickBatch (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        DeliveryOrderId nvarchar(50) NULL,
        ProductId nvarchar(50) NULL,
        BatchId nvarchar(50) NULL,
        Quantity float NULL
    );
    CREATE INDEX IX_DeliveryPickBatch_DeliveryOrderId ON dbo.DeliveryPickBatch(DeliveryOrderId);
    CREATE INDEX IX_DeliveryPickBatch_BatchId ON dbo.DeliveryPickBatch(BatchId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("DeliveryPickBatch table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.AutomationRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AutomationRule (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        RuleType nvarchar(50) NULL,
        Name nvarchar(255) NULL,
        Enabled bit NOT NULL DEFAULT 1,
        Threshold float NULL,
        NotifyInApp bit NOT NULL DEFAULT 1,
        EmailRecipients nvarchar(4000) NULL,
        LastCheckedUtc datetime2 NULL,
        LastCount int NULL,
        LastEmailDay nvarchar(50) NULL
    );

END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("AutomationRule table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.AppNotification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppNotification (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        RuleId nvarchar(50) NULL,
        RuleType nvarchar(50) NULL,
        Severity nvarchar(50) NULL,
        Title nvarchar(255) NULL,
        Message nvarchar(4000) NULL,
        Link nvarchar(255) NULL,
        ItemCount int NULL,
        DayKey nvarchar(50) NULL,
        ReadByIds nvarchar(4000) NULL
    );
    CREATE INDEX IX_AppNotification_RuleId ON dbo.AppNotification(RuleId);
    CREATE INDEX IX_AppNotification_DayKey ON dbo.AppNotification(DayKey);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("AppNotification table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ScheduledReport', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduledReport (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Name nvarchar(255) NULL,
        ReportType nvarchar(50) NULL,
        Frequency nvarchar(50) NULL,
        DayOfWeek int NULL,
        DayOfMonth int NULL,
        SendHour int NOT NULL DEFAULT 8,
        SendMinute int NOT NULL DEFAULT 0,
        Recipients nvarchar(4000) NULL,
        Enabled bit NOT NULL DEFAULT 1,
        WarehouseId nvarchar(50) NULL,
        SlowDays int NULL,
        LastSentUtc datetime2 NULL,
        LastStatus nvarchar(255) NULL
    );
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ScheduledReport table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.StockHold', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockHold (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        ProductId nvarchar(50) NULL,
        WarehouseId nvarchar(50) NULL,
        Quantity float NULL,
        OriginalQuantity float NULL,
        Reason nvarchar(50) NULL,
        Notes nvarchar(500) NULL,
        Status nvarchar(20) NULL,
        ReleasedAtUtc datetime2 NULL,
        ReleasedById nvarchar(450) NULL
    );
    CREATE INDEX IX_StockHold_ProductId ON dbo.StockHold(ProductId);
    CREATE INDEX IX_StockHold_Status ON dbo.StockHold(Status);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("StockHold table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.WarehouseTask', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WarehouseTask (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Number nvarchar(50) NULL,
        TaskType nvarchar(20) NULL,
        Title nvarchar(200) NULL,
        Reference nvarchar(100) NULL,
        WarehouseId nvarchar(50) NULL,
        Priority nvarchar(20) NULL,
        AssignedToId nvarchar(450) NULL,
        Status nvarchar(20) NULL,
        DueAtUtc datetime2 NULL,
        StartedAtUtc datetime2 NULL,
        CompletedAtUtc datetime2 NULL,
        Notes nvarchar(500) NULL
    );
    CREATE INDEX IX_WarehouseTask_Status ON dbo.WarehouseTask(Status);
    CREATE INDEX IX_WarehouseTask_AssignedToId ON dbo.WarehouseTask(AssignedToId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("WarehouseTask table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.PortalResponse', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalResponse (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        PortalLinkId nvarchar(50) NULL,
        Status nvarchar(20) NULL,
        ExpectedDate datetime2 NULL,
        Message nvarchar(1000) NULL
    );
    CREATE INDEX IX_PortalResponse_PortalLinkId ON dbo.PortalResponse(PortalLinkId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("PortalResponse table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.PortalLink', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalLink (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        EntityType nvarchar(30) NULL,
        EntityId nvarchar(50) NULL,
        Token nvarchar(64) NULL,
        ExpiresAtUtc datetime2 NULL,
        IsActive bit NULL
    );
    CREATE INDEX IX_PortalLink_Token ON dbo.PortalLink(Token);
    CREATE INDEX IX_PortalLink_EntityId ON dbo.PortalLink(EntityId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("PortalLink table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"IF OBJECT_ID(N'dbo.Shipment', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Shipment', N'PublicToken') IS NULL ALTER TABLE dbo.Shipment ADD PublicToken nvarchar(64) NULL");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Shipment PublicToken check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.WebhookEndpoint', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WebhookEndpoint (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Name nvarchar(100) NULL,
        Url nvarchar(500) NULL,
        Secret nvarchar(100) NULL,
        Events nvarchar(300) NULL,
        IsActive bit NULL,
        LastStatus nvarchar(300) NULL,
        LastAtUtc datetime2 NULL
    );
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("WebhookEndpoint table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.IntegrationApiKey', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IntegrationApiKey (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Name nvarchar(100) NULL,
        KeyPrefix nvarchar(20) NULL,
        KeyHash nvarchar(100) NULL,
        IsActive bit NULL,
        LastUsedAtUtc datetime2 NULL
    );
    CREATE INDEX IX_IntegrationApiKey_KeyHash ON dbo.IntegrationApiKey(KeyHash);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("IntegrationApiKey table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.DockAppointment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DockAppointment (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Number nvarchar(50) NULL,
        DockDoorId nvarchar(50) NULL,
        Direction nvarchar(20) NULL,
        StartAtUtc datetime2 NULL,
        DurationMinutes int NULL,
        Carrier nvarchar(200) NULL,
        Reference nvarchar(200) NULL,
        PartyName nvarchar(200) NULL,
        Status nvarchar(20) NULL,
        Notes nvarchar(500) NULL,
        CheckedInAtUtc datetime2 NULL
    );
    CREATE INDEX IX_DockAppointment_DockDoorId ON dbo.DockAppointment(DockDoorId);
    CREATE INDEX IX_DockAppointment_StartAtUtc ON dbo.DockAppointment(StartAtUtc);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("DockAppointment table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.DockDoor', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DockDoor (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Name nvarchar(100) NULL,
        WarehouseId nvarchar(50) NULL,
        DoorType nvarchar(20) NULL,
        IsActive bit NULL
    );
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("DockDoor table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.KitAssembly', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KitAssembly (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Number nvarchar(50) NULL,
        KitProductId nvarchar(50) NULL,
        WarehouseId nvarchar(50) NULL,
        Quantity float NULL,
        ComponentsDocument nvarchar(50) NULL,
        KitsDocument nvarchar(50) NULL,
        Notes nvarchar(500) NULL
    );
    CREATE INDEX IX_KitAssembly_KitProductId ON dbo.KitAssembly(KitProductId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("KitAssembly table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ProductBomLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductBomLine (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        KitProductId nvarchar(50) NULL,
        ComponentProductId nvarchar(50) NULL,
        Quantity float NULL
    );
    CREATE INDEX IX_ProductBomLine_KitProductId ON dbo.ProductBomLine(KitProductId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductBomLine table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.PickWaveOrder', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PickWaveOrder (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        PickWaveId nvarchar(50) NULL,
        DeliveryOrderId nvarchar(50) NULL
    );
    CREATE INDEX IX_PickWaveOrder_PickWaveId ON dbo.PickWaveOrder(PickWaveId);
    CREATE INDEX IX_PickWaveOrder_DeliveryOrderId ON dbo.PickWaveOrder(DeliveryOrderId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("PickWaveOrder table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.PickWave', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PickWave (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Number nvarchar(50) NULL,
        Status nvarchar(20) NULL,
        Notes nvarchar(500) NULL
    );
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("PickWave table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.PermissionRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PermissionRule (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        RoleName nvarchar(256) NULL,
        Module nvarchar(100) NULL,
        ActionKey nvarchar(20) NULL
    );
    CREATE INDEX IX_PermissionRule_RoleName ON dbo.PermissionRule(RoleName);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("PermissionRule table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.LandedCost', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LandedCost (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        GoodsReceiveId nvarchar(50) NULL,
        Description nvarchar(255) NULL,
        Amount float NULL,
        Basis nvarchar(20) NULL
    );
    CREATE INDEX IX_LandedCost_GoodsReceiveId ON dbo.LandedCost(GoodsReceiveId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("LandedCost table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ReplenishmentRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReplenishmentRule (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        LocationId nvarchar(50) NULL,
        ProductId nvarchar(50) NULL,
        MinQuantity float NULL,
        MaxQuantity float NULL
    );
    CREATE INDEX IX_ReplenishmentRule_LocationId ON dbo.ReplenishmentRule(LocationId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ReplenishmentRule table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.LocationMove', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LocationMove (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        ProductId nvarchar(50) NULL,
        FromLocationId nvarchar(50) NULL,
        ToLocationId nvarchar(50) NULL,
        Quantity float NULL,
        MoveType nvarchar(20) NULL,
        Reason nvarchar(255) NULL
    );
    CREATE INDEX IX_LocationMove_ProductId ON dbo.LocationMove(ProductId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("LocationMove table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ReturnInspection', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReturnInspection (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        SalesReturnId nvarchar(50) NULL,
        ProductId nvarchar(50) NULL,
        ReturnedQuantity float NULL,
        GoodQuantity float NULL,
        DamagedQuantity float NULL,
        ReasonCode nvarchar(50) NULL,
        Note nvarchar(500) NULL,
        LocationId nvarchar(50) NULL
    );
    CREATE INDEX IX_ReturnInspection_SalesReturnId ON dbo.ReturnInspection(SalesReturnId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ReturnInspection table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ShipmentPackageItem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShipmentPackageItem (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        ShipmentId nvarchar(50) NULL,
        ShipmentPackageId nvarchar(50) NULL,
        ProductId nvarchar(50) NULL,
        Quantity float NULL
    );
    CREATE INDEX IX_ShipmentPackageItem_ShipmentId ON dbo.ShipmentPackageItem(ShipmentId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ShipmentPackageItem table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ShipmentPackage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShipmentPackage (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        ShipmentId nvarchar(50) NULL,
        PackageNumber int NULL,
        PackageType nvarchar(50) NULL,
        WeightKg float NULL,
        LengthCm float NULL,
        WidthCm float NULL,
        HeightCm float NULL
    );
    CREATE INDEX IX_ShipmentPackage_ShipmentId ON dbo.ShipmentPackage(ShipmentId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ShipmentPackage table check failed: " + ex.Message);
            }

            try
            {
                dataContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.Shipment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Shipment (
        Id nvarchar(50) NOT NULL PRIMARY KEY,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedAtUtc datetime2 NULL,
        CreatedById nvarchar(450) NULL,
        UpdatedAtUtc datetime2 NULL,
        UpdatedById nvarchar(450) NULL,
        Number nvarchar(50) NULL,
        DeliveryOrderId nvarchar(50) NULL,
        Status nvarchar(20) NULL,
        Carrier nvarchar(100) NULL,
        TrackingNumber nvarchar(100) NULL,
        Notes nvarchar(500) NULL,
        ShippedAtUtc datetime2 NULL,
        ShippedById nvarchar(450) NULL,
        PublicToken nvarchar(64) NULL
    );
    CREATE INDEX IX_Shipment_DeliveryOrderId ON dbo.Shipment(DeliveryOrderId);
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Shipment table check failed: " + ex.Message);
            }
        }

        return host;
    }
}


