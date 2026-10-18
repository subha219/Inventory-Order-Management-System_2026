using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DashboardManager.Queries;

public class WorkMetricDto
{
    public string? Label { get; init; }
    public double Value { get; init; }
    /// <summary>good, warn, bad or empty.</summary>
    public string? Tone { get; init; }
    public string? Link { get; init; }
}

public class WorkWidgetDto
{
    public string? Key { get; init; }
    public string? Title { get; init; }
    public string? Icon { get; init; }
    public string? Link { get; init; }
    public List<WorkMetricDto> Metrics { get; init; } = new();
}

public class GetMyWorkResult
{
    /// <summary>admin, manager, finance, operator, controller, buyer, sales or general.</summary>
    public string Persona { get; init; } = "general";
    public List<WorkWidgetDto> Widgets { get; init; } = new();
}

public class GetMyWorkRequest : IRequest<GetMyWorkResult>
{
    public List<string> Roles { get; init; } = new();
    public string? UserId { get; init; }
}

/// <summary>Small, role-aware to-do counts for the dashboard: each person only gets the widgets their roles allow.</summary>
public class GetMyWorkHandler : IRequestHandler<GetMyWorkRequest, GetMyWorkResult>
{
    private readonly IQueryContext _context;

    public GetMyWorkHandler(IQueryContext context)
    {
        _context = context;
    }

    private static string? Tone(double v, bool badWhenPositive = false) => v <= 0 ? "good" : badWhenPositive ? "bad" : "warn";

    public async Task<GetMyWorkResult> Handle(GetMyWorkRequest r, CancellationToken ct)
    {
        var roles = new HashSet<string>(r.Roles ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        bool Has(params string[] names) => names.Any(roles.Contains);
        var now = DateTime.UtcNow;
        var today = now.Date;
        var widgets = new List<WorkWidgetDto>();

        // ---- warehouse floor
        if (Has("Warehouses"))
        {
            var uid = r.UserId;
            var myTasks = await _context.WarehouseTask.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(t => (t.Status == "Open" || t.Status == "InProgress") && (t.AssignedToId == uid || t.AssignedToId == null)).ToListAsync(ct);
            var openWaves = await _context.PickWave.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(w => w.Status == "Open", ct);
            var packing = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.Status == "Packing", ct);
            var toPick = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(d => d.Status == DeliveryOrderStatus.Draft || d.Status == DeliveryOrderStatus.Confirmed, ct);
            widgets.Add(new WorkWidgetDto
            {
                Key = "floor", Title = "Warehouse floor", Icon = "fa-dolly", Link = "/Warehouses/Tasks", Metrics =
                {
                    new WorkMetricDto { Label = "My open tasks", Value = myTasks.Count, Tone = myTasks.Count == 0 ? "good" : null, Link = "/Warehouses/Tasks" },
                    new WorkMetricDto { Label = "Overdue tasks", Value = myTasks.Count(t => t.DueAtUtc != null && t.DueAtUtc < now), Tone = myTasks.Any(t => t.DueAtUtc != null && t.DueAtUtc < now) ? "bad" : "good", Link = "/Warehouses/Tasks" },
                    new WorkMetricDto { Label = "Orders to pick or ship", Value = toPick, Link = "/Warehouses/PickList" },
                    new WorkMetricDto { Label = "Open pick waves", Value = openWaves, Link = "/Warehouses/WavePicking" },
                    new WorkMetricDto { Label = "Shipments being packed", Value = packing, Link = "/Warehouses/Packing" }
                }
            });

            var start = today; var end = today.AddDays(1);
            var appts = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false).Where(a => a.StartAtUtc >= start && a.StartAtUtc < end).ToListAsync(ct);
            var late = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(a => a.Status == "Scheduled" && a.StartAtUtc < now.AddMinutes(-15), ct);
            var holds = await _context.StockHold.AsNoTracking().ApplyIsDeletedFilter(false).Where(h => h.Status == "Active").Select(h => h.Quantity ?? 0).ToListAsync(ct);
            widgets.Add(new WorkWidgetDto
            {
                Key = "dock", Title = "Dock and holds", Icon = "fa-truck-loading", Link = "/Warehouses/DockSchedule", Metrics =
                {
                    new WorkMetricDto { Label = "Trucks today", Value = appts.Count(a => a.Status != "Cancelled"), Link = "/Warehouses/DockSchedule" },
                    new WorkMetricDto { Label = "Trucks on site", Value = appts.Count(a => a.Status == "CheckedIn"), Link = "/Warehouses/DockSchedule" },
                    new WorkMetricDto { Label = "Trucks late", Value = late, Tone = Tone(late, true), Link = "/Warehouses/DockSchedule" },
                    new WorkMetricDto { Label = "Units on hold", Value = holds.Sum(), Tone = holds.Count > 0 ? "warn" : "good", Link = "/Warehouses/StockHolds" }
                }
            });
        }

        // ---- selling side
        if (Has("SalesOrders", "DeliveryOrders", "SalesReports", "Customers"))
        {
            var confirmed = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.OrderStatus == SalesOrderStatus.Confirmed).Select(s => s.Id).ToListAsync(ct);
            var delivered = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(d => d.Status != DeliveryOrderStatus.Cancelled && confirmed.Contains(d.SalesOrderId!)).Select(d => d.SalesOrderId).Distinct().ToListAsync(ct);
            var drafts = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.OrderStatus == SalesOrderStatus.Draft, ct);
            var todayOrders = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.OrderDate >= today && s.OrderDate < today.AddDays(1), ct);
            var waitingDo = confirmed.Count - delivered.Count;
            widgets.Add(new WorkWidgetDto
            {
                Key = "sales", Title = "Sales", Icon = "fa-shopping-bag", Link = "/SalesOrders/SalesOrderList", Metrics =
                {
                    new WorkMetricDto { Label = "Orders today", Value = todayOrders, Link = "/SalesOrders/SalesOrderList" },
                    new WorkMetricDto { Label = "Draft orders to confirm", Value = drafts, Tone = drafts > 0 ? "warn" : "good", Link = "/SalesOrders/SalesOrderList" },
                    new WorkMetricDto { Label = "Confirmed, no delivery yet", Value = waitingDo, Tone = waitingDo > 0 ? "warn" : "good", Link = "/DeliveryOrders/DeliveryOrderList" }
                }
            });
        }

        // ---- buying side
        if (Has("PurchaseOrders", "GoodsReceives", "PurchaseReports", "Vendors"))
        {
            var confirmed = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.OrderStatus == PurchaseOrderStatus.Confirmed).Select(p => p.Id).ToListAsync(ct);
            var received = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(g => g.Status != GoodsReceiveStatus.Cancelled && confirmed.Contains(g.PurchaseOrderId!)).Select(g => g.PurchaseOrderId).Distinct().ToListAsync(ct);
            var drafts = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(p => p.OrderStatus == PurchaseOrderStatus.Draft, ct);
            var draftGr = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(g => g.Status == GoodsReceiveStatus.Draft, ct);
            var waiting = confirmed.Count - received.Count;
            widgets.Add(new WorkWidgetDto
            {
                Key = "purchase", Title = "Purchasing", Icon = "fa-truck-loading", Link = "/PurchaseOrders/PurchaseOrderList", Metrics =
                {
                    new WorkMetricDto { Label = "Draft purchase orders", Value = drafts, Tone = drafts > 0 ? "warn" : "good", Link = "/PurchaseOrders/PurchaseOrderList" },
                    new WorkMetricDto { Label = "Confirmed, nothing received", Value = waiting, Tone = waiting > 0 ? "warn" : "good", Link = "/GoodsReceives/GoodsReceiveList" },
                    new WorkMetricDto { Label = "Receipts to confirm", Value = draftGr, Tone = draftGr > 0 ? "warn" : "good", Link = "/GoodsReceives/GoodsReceiveList" }
                }
            });
        }

        // ---- stock health
        if (Has("Products", "StockReports", "Warehouses", "StockCounts", "PurchaseOrders"))
        {
            var stockRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false)
                .GroupBy(x => x.ProductId).Select(g => new { Id = g.Key, Stock = g.Sum(x => x.Stock) }).ToListAsync(ct);
            var stock = stockRows.Where(x => x.Id != null).ToDictionary(x => x.Id!, x => x.Stock ?? 0.0);
            var rp = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.Physical == true && (p.ReorderPoint ?? 0) > 0).Select(p => new { p.Id, p.ReorderPoint }).ToListAsync(ct);
            var low = rp.Count(p => (stock.TryGetValue(p.Id, out var s) ? s : 0) <= p.ReorderPoint);
            var soon = today.AddDays(30);
            var expiring = await _context.ProductBatch.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(b => b.ExpiryDate != null && b.ExpiryDate <= soon && (b.Quantity ?? 0) > 0, ct);
            var openCounts = await _context.StockCount.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(c => c.Status == StockCountStatus.Draft, ct);
            widgets.Add(new WorkWidgetDto
            {
                Key = "stock", Title = "Stock health", Icon = "fa-boxes", Link = "/PurchaseOrders/ReorderSuggestions", Metrics =
                {
                    new WorkMetricDto { Label = "Items at or below reorder point", Value = low, Tone = Tone(low), Link = "/PurchaseOrders/ReorderSuggestions" },
                    new WorkMetricDto { Label = "Batches expiring in 30 days", Value = expiring, Tone = Tone(expiring, true), Link = "/Products/ProductBatches" },
                    new WorkMetricDto { Label = "Stock counts in progress", Value = openCounts, Link = "/StockCounts/StockCountList" }
                }
            });
        }

        // ---- administration
        if (Has("Users"))
        {
            var pending = await _context.ApprovalRequest.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(a => a.Status == "Pending", ct);
            var failing = await _context.WebhookEndpoint.AsNoTracking().ApplyIsDeletedFilter(false)
                .CountAsync(h => h.IsActive == true && h.LastStatus != null && h.LastStatus.StartsWith("Failed"), ct);
            var responses = await _context.PortalResponse.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(p => p.CreatedAtUtc >= today.AddDays(-7), ct);
            widgets.Add(new WorkWidgetDto
            {
                Key = "admin", Title = "Administration", Icon = "fa-user-shield", Link = "/Users/Approvals", Metrics =
                {
                    new WorkMetricDto { Label = "Approvals waiting", Value = pending, Tone = Tone(pending), Link = "/Users/Approvals" },
                    new WorkMetricDto { Label = "Failing webhooks", Value = failing, Tone = Tone(failing, true), Link = "/Users/Integrations" },
                    new WorkMetricDto { Label = "Partner replies, last 7 days", Value = responses, Link = "/PurchaseOrders/Portal" }
                }
            });
        }

        // Each job role gets its own dashboard: only the widgets that belong to that job.
        string persona;
        string[]? allowed;
        if (Has("Administrator", "Users")) { persona = "admin"; allowed = new[] { "admin" }; }
        else if (Has("Manager")) { persona = "manager"; allowed = new[] { "floor", "dock", "sales", "purchase", "stock" }; }
        else if (Has("Finance / Accountant")) { persona = "finance"; allowed = new[] { "sales", "purchase", "stock" }; }
        else if (Has("Warehouse Operator")) { persona = "operator"; allowed = new[] { "floor", "dock" }; }
        else if (Has("Inventory Controller")) { persona = "controller"; allowed = new[] { "stock", "dock" }; }
        else if (Has("Buyer / Purchasing")) { persona = "buyer"; allowed = new[] { "purchase", "stock" }; }
        else if (Has("Sales")) { persona = "sales"; allowed = new[] { "sales" }; }
        else { persona = "general"; allowed = null; }

        if (allowed != null) widgets = widgets.Where(w => w.Key != null && allowed.Contains(w.Key)).ToList();
        return new GetMyWorkResult { Persona = persona, Widgets = widgets };
    }
}
