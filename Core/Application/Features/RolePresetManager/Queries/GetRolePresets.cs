using Application.Common.Services.SecurityManager;
using MediatR;

namespace Application.Features.RolePresetManager.Queries;

public static class RolePresetCatalog
{
    public static readonly (string Key, string Name, string Description, string[] Roles)[] All =
    {
        ("warehouse-operator", "Warehouse Operator", "Floor staff: tasks, receiving and put-away, picking, packing, shipping, bin moves, counts and returns.",
            new[] { "Warehouses", "Products", "GoodsReceives", "DeliveryOrders", "TransferIns", "TransferOuts", "StockCounts", "SalesReturns", "PurchaseReturns", "StockReports", "MovementReports", "Todos", "TodoItems" }),
        ("inventory-controller", "Inventory Controller", "Stock accuracy: product master data, batches, serials, adjustments, scrapping, cycle counts, holds, costing and stock reports.",
            new[] { "Products", "ProductGroups", "UnitMeasures", "Warehouses", "StockCounts", "PositiveAdjustments", "NegativeAdjustments", "Scrappings", "TransferIns", "TransferOuts", "StockReports", "MovementReports", "TransactionReports", "Todos", "TodoItems" }),
        ("buyer", "Buyer / Purchasing", "Procurement: reorder suggestions, purchase orders, vendors, partner portals, supplier scorecard, goods receipts and purchase returns.",
            new[] { "PurchaseOrders", "PurchaseReports", "Vendors", "VendorGroups", "VendorCategories", "VendorContacts", "GoodsReceives", "PurchaseReturns", "Products", "StockReports", "MovementReports", "Todos", "TodoItems" }),
        ("sales", "Sales", "Sales and customer service: customers, sales orders, backorders, delivery orders, sales returns and sales reports.",
            new[] { "SalesOrders", "SalesReports", "Customers", "CustomerGroups", "CustomerCategories", "CustomerContacts", "DeliveryOrders", "SalesReturns", "Products", "StockReports", "Todos", "TodoItems" }),
        ("finance", "Finance / Accountant", "Numbers and reports: dashboard analytics, costing and landed cost, sales, purchase, stock, movement and transaction reports, taxes.",
            new[] { "Dashboards", "SalesReports", "PurchaseReports", "StockReports", "MovementReports", "TransactionReports", "Taxs", "Todos", "TodoItems" }),
        ("warehouse-supervisor", "Warehouse Supervisor", "Leads the floor: assigns and chases tasks, watches late deliveries, dock and packing performance, counts and accuracy.",
            new[] { "Warehouses", "Products", "GoodsReceives", "DeliveryOrders", "TransferIns", "TransferOuts", "StockCounts", "SalesReturns", "PurchaseReturns", "StockReports", "MovementReports", "TransactionReports", "Todos", "TodoItems" }),
        ("quality-inspector", "Quality / Returns Inspector", "Quality: returns inspection, stock holds and quarantine, expiring batches, recall tracing, scrapping and return reasons.",
            new[] { "Warehouses", "Products", "SalesReturns", "PurchaseReturns", "Scrappings", "StockReports", "MovementReports", "Todos", "TodoItems" }),
        ("customer-service", "Customer Service", "Answers customers: order and delivery status, late deliveries, shipments and tracking, returns. No stock changes.",
            new[] { "Customers", "CustomerContacts", "SalesOrders", "DeliveryOrders", "SalesReturns", "SalesReports", "Products", "Todos", "TodoItems" }),
        ("auditor", "Auditor / Read-only", "Looks but never changes: adjustments, write-offs, count differences, deletions, reports and the audit log. Every change is refused by the server.",
            new[] { "Dashboards", "Products", "ProductGroups", "UnitMeasures", "Warehouses", "StockCounts", "PositiveAdjustments", "NegativeAdjustments", "Scrappings", "TransferIns", "TransferOuts", "GoodsReceives", "DeliveryOrders", "SalesReturns", "PurchaseReturns", "SalesOrders", "PurchaseOrders", "Customers", "Vendors", "SalesReports", "PurchaseReports", "StockReports", "MovementReports", "TransactionReports", "Taxs" }),
        ("manager", "Manager", "Runs the operation: full dashboard and analytics, all sales, purchase and inventory areas and all reports. No user, role or system settings.",
            new[] { "Dashboards", "Warehouses", "Products", "ProductGroups", "UnitMeasures", "SalesOrders", "Customers", "CustomerGroups", "CustomerCategories", "CustomerContacts", "PurchaseOrders", "Vendors", "VendorGroups", "VendorCategories", "VendorContacts", "GoodsReceives", "DeliveryOrders", "SalesReturns", "PurchaseReturns", "TransferIns", "TransferOuts", "PositiveAdjustments", "NegativeAdjustments", "Scrappings", "StockCounts", "SalesReports", "PurchaseReports", "StockReports", "MovementReports", "TransactionReports", "Todos", "TodoItems" }),
    };
}

public class RolePresetDto
{
    public string? Key { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public List<string> Roles { get; init; } = new();
    public List<string> MissingRoles { get; init; } = new();
    public string? TestEmail { get; init; }
}

public class GetRolePresetsResult
{
    public List<RolePresetDto> Presets { get; init; } = new();
}

public class GetRolePresetsRequest : IRequest<GetRolePresetsResult>
{
}

public class GetRolePresetsHandler : IRequestHandler<GetRolePresetsRequest, GetRolePresetsResult>
{
    private readonly ISecurityService _security;

    public GetRolePresetsHandler(ISecurityService security)
    {
        _security = security;
    }

    public async Task<GetRolePresetsResult> Handle(GetRolePresetsRequest request, CancellationToken cancellationToken)
    {
        var existing = (await _security.GetRoleListAsync(cancellationToken))
            .Select(r => r.Name ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new GetRolePresetsResult();
        foreach (var p in RolePresetCatalog.All)
        {
            result.Presets.Add(new RolePresetDto
            {
                Key = p.Key,
                Name = p.Name,
                Description = p.Description,
                Roles = p.Roles.ToList(),
                MissingRoles = existing.Contains(p.Name) ? new List<string>() : new List<string> { p.Name + " (role row not created yet - restart the app)" },
                TestEmail = p.Key + ".test@example.com"
            });
        }
        return result;
    }
}
