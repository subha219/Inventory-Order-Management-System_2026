using Application.Common.Repositories;
using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Configurations;
using Infrastructure.SecurityManager.AspNetIdentity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DataAccessManager.EFCore.Contexts;


public class DataContext : IdentityDbContext<ApplicationUser>, IEntityDbSet
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
    }

    public DbSet<Token> Token { get; set; }
    public DbSet<Todo> Todo { get; set; }
    public DbSet<TodoItem> TodoItem { get; set; }
    public DbSet<Company> Company { get; set; }
    public DbSet<FileImage> FileImage { get; set; }
    public DbSet<FileDocument> FileDocument { get; set; }


    public DbSet<NumberSequence> NumberSequence { get; set; }
    public DbSet<CustomerGroup> CustomerGroup { get; set; }
    public DbSet<CustomerCategory> CustomerCategory { get; set; }
    public DbSet<VendorGroup> VendorGroup { get; set; }
    public DbSet<VendorCategory> VendorCategory { get; set; }
    public DbSet<Warehouse> Warehouse { get; set; }
    public DbSet<Customer> Customer { get; set; }
    public DbSet<Vendor> Vendor { get; set; }
    public DbSet<UnitMeasure> UnitMeasure { get; set; }
    public DbSet<ProductGroup> ProductGroup { get; set; }
    public DbSet<Product> Product { get; set; }
    public DbSet<ProductBatch> ProductBatch { get; set; }
    public DbSet<AuditLog> AuditLog { get; set; }
    public DbSet<StorageLocation> StorageLocation { get; set; }
    public DbSet<LocationStock> LocationStock { get; set; }
    public DbSet<ApprovalRequest> ApprovalRequest { get; set; }
    public DbSet<DeliveryPick> DeliveryPick { get; set; }
    public DbSet<QualityInspection> QualityInspection { get; set; }
    public DbSet<PutAwayRecord> PutAwayRecord { get; set; }
    public DbSet<ProductSerial> ProductSerial { get; set; }
    public DbSet<DeliveryPickBatch> DeliveryPickBatch { get; set; }
    public DbSet<AutomationRule> AutomationRule { get; set; }
    public DbSet<AppNotification> AppNotification { get; set; }
    public DbSet<ScheduledReport> ScheduledReport { get; set; }
    public DbSet<StockHold> StockHold { get; set; }
    public DbSet<WarehouseTask> WarehouseTask { get; set; }
    public DbSet<PortalResponse> PortalResponse { get; set; }
    public DbSet<PortalLink> PortalLink { get; set; }
    public DbSet<WebhookEndpoint> WebhookEndpoint { get; set; }
    public DbSet<IntegrationApiKey> IntegrationApiKey { get; set; }
    public DbSet<DockAppointment> DockAppointment { get; set; }
    public DbSet<DockDoor> DockDoor { get; set; }
    public DbSet<KitAssembly> KitAssembly { get; set; }
    public DbSet<ProductBomLine> ProductBomLine { get; set; }
    public DbSet<PickWaveOrder> PickWaveOrder { get; set; }
    public DbSet<PickWave> PickWave { get; set; }
    public DbSet<PermissionRule> PermissionRule { get; set; }
    public DbSet<LandedCost> LandedCost { get; set; }
    public DbSet<ReplenishmentRule> ReplenishmentRule { get; set; }
    public DbSet<LocationMove> LocationMove { get; set; }
    public DbSet<ReturnInspection> ReturnInspection { get; set; }
    public DbSet<ShipmentPackageItem> ShipmentPackageItem { get; set; }
    public DbSet<ShipmentPackage> ShipmentPackage { get; set; }
    public DbSet<Shipment> Shipment { get; set; }
    public DbSet<CustomerContact> CustomerContact { get; set; }
    public DbSet<VendorContact> VendorContact { get; set; }
    public DbSet<Tax> Tax { get; set; }
    public DbSet<SalesOrder> SalesOrder { get; set; }
    public DbSet<SalesOrderItem> SalesOrderItem { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrder { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItem { get; set; }
    public DbSet<InventoryTransaction> InventoryTransaction { get; set; }
    public DbSet<DeliveryOrder> DeliveryOrder { get; set; }
    public DbSet<GoodsReceive> GoodsReceive { get; set; }
    public DbSet<SalesReturn> SalesReturn { get; set; }
    public DbSet<PurchaseReturn> PurchaseReturn { get; set; }
    public DbSet<TransferIn> TransferIn { get; set; }
    public DbSet<TransferOut> TransferOut { get; set; }
    public DbSet<StockCount> StockCount { get; set; }
    public DbSet<NegativeAdjustment> NegativeAdjustment { get; set; }
    public DbSet<PositiveAdjustment> PositiveAdjustment { get; set; }
    public DbSet<Scrapping> Scrapping { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new TokenConfiguration());
        modelBuilder.ApplyConfiguration(new TodoConfiguration());
        modelBuilder.ApplyConfiguration(new TodoItemConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyConfiguration());
        modelBuilder.ApplyConfiguration(new FileImageConfiguration());
        modelBuilder.ApplyConfiguration(new FileDocumentConfiguration());


        modelBuilder.ApplyConfiguration(new NumberSequenceConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerGroupConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new VendorGroupConfiguration());
        modelBuilder.ApplyConfiguration(new VendorCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new WarehouseConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new VendorConfiguration());
        modelBuilder.ApplyConfiguration(new UnitMeasureConfiguration());
        modelBuilder.ApplyConfiguration(new ProductGroupConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductBatchConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new StorageLocationConfiguration());
        modelBuilder.ApplyConfiguration(new LocationStockConfiguration());
        modelBuilder.ApplyConfiguration(new ApprovalRequestConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryPickConfiguration());
        modelBuilder.ApplyConfiguration(new QualityInspectionConfiguration());
        modelBuilder.ApplyConfiguration(new PutAwayRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ProductSerialConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryPickBatchConfiguration());
        modelBuilder.ApplyConfiguration(new AutomationRuleConfiguration());
        modelBuilder.ApplyConfiguration(new AppNotificationConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduledReportConfiguration());
        modelBuilder.ApplyConfiguration(new StockHoldConfiguration());
        modelBuilder.ApplyConfiguration(new WarehouseTaskConfiguration());
        modelBuilder.ApplyConfiguration(new PortalResponseConfiguration());
        modelBuilder.ApplyConfiguration(new PortalLinkConfiguration());
        modelBuilder.ApplyConfiguration(new WebhookEndpointConfiguration());
        modelBuilder.ApplyConfiguration(new IntegrationApiKeyConfiguration());
        modelBuilder.ApplyConfiguration(new DockAppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new DockDoorConfiguration());
        modelBuilder.ApplyConfiguration(new KitAssemblyConfiguration());
        modelBuilder.ApplyConfiguration(new ProductBomLineConfiguration());
        modelBuilder.ApplyConfiguration(new PickWaveOrderConfiguration());
        modelBuilder.ApplyConfiguration(new PickWaveConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionRuleConfiguration());
        modelBuilder.ApplyConfiguration(new LandedCostConfiguration());
        modelBuilder.ApplyConfiguration(new ReplenishmentRuleConfiguration());
        modelBuilder.ApplyConfiguration(new LocationMoveConfiguration());
        modelBuilder.ApplyConfiguration(new ReturnInspectionConfiguration());
        modelBuilder.ApplyConfiguration(new ShipmentPackageItemConfiguration());
        modelBuilder.ApplyConfiguration(new ShipmentPackageConfiguration());
        modelBuilder.ApplyConfiguration(new ShipmentConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerContactConfiguration());
        modelBuilder.ApplyConfiguration(new VendorContactConfiguration());
        modelBuilder.ApplyConfiguration(new TaxConfiguration());
        modelBuilder.ApplyConfiguration(new SalesOrderConfiguration());
        modelBuilder.ApplyConfiguration(new SalesOrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseOrderConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseOrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryOrderConfiguration());
        modelBuilder.ApplyConfiguration(new GoodsReceiveConfiguration());
        modelBuilder.ApplyConfiguration(new SalesReturnConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseReturnConfiguration());
        modelBuilder.ApplyConfiguration(new TransferInConfiguration());
        modelBuilder.ApplyConfiguration(new TransferOutConfiguration());
        modelBuilder.ApplyConfiguration(new StockCountConfiguration());
        modelBuilder.ApplyConfiguration(new NegativeAdjustmentConfiguration());
        modelBuilder.ApplyConfiguration(new PositiveAdjustmentConfiguration());
        modelBuilder.ApplyConfiguration(new ScrappingConfiguration());

    }

}


