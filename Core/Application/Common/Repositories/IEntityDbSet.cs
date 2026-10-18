using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Repositories;


public interface IEntityDbSet
{
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
}

