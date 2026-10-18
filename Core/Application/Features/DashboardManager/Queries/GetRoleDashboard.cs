using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DashboardManager.Queries;

public class DashKpiDto
{
    public string? Label { get; init; }
    public double? Value { get; init; }
    /// <summary>"" for a plain number, "money", "%", "days", "hours".</summary>
    public string? Unit { get; init; }
    public string? Note { get; init; }
    /// <summary>good, warn, bad or empty.</summary>
    public string? Tone { get; init; }
    public string? Link { get; init; }
    public List<double> Spark { get; init; } = new();
}

public class DashSeriesDto
{
    public string? Name { get; init; }
    public List<double> Values { get; init; } = new();
}

public class DashChartDto
{
    public string? Key { get; init; }
    public string? Title { get; init; }
    /// <summary>line, bar, hbar or donut.</summary>
    public string? Type { get; init; }
    public string? Unit { get; init; }
    public string? Link { get; init; }
    public List<string> Labels { get; init; } = new();
    public List<DashSeriesDto> Series { get; init; } = new();
}

public class DashRowDto
{
    public List<string> Cells { get; init; } = new();
    public string? Link { get; init; }
    public string? Tone { get; init; }
}

public class DashListDto
{
    public string? Key { get; init; }
    public string? Title { get; init; }
    public string? Link { get; init; }
    public string? Empty { get; init; }
    public List<string> Columns { get; init; } = new();
    public List<DashRowDto> Rows { get; init; } = new();
}

public class GetRoleDashboardResult
{
    public string Persona { get; init; } = "general";
    public List<DashKpiDto> Kpis { get; init; } = new();
    public List<DashChartDto> Charts { get; init; } = new();
    public List<DashListDto> Lists { get; init; } = new();
}

public class GetRoleDashboardRequest : IRequest<GetRoleDashboardResult>
{
    public List<string> Roles { get; init; } = new();
    public string? UserId { get; init; }
}

/// <summary>One data set per job: headline numbers, trends and to-do lists, so each role gets its own dashboard.</summary>
public class GetRoleDashboardHandler : IRequestHandler<GetRoleDashboardRequest, GetRoleDashboardResult>
{
    private readonly IQueryContext _context;

    public GetRoleDashboardHandler(IQueryContext context)
    {
        _context = context;
    }

    private class ProductRow
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public double Reorder { get; set; }
        public double ReorderQty { get; set; }
        public double Price { get; set; }
        public double Qty { get; set; }
    }

    private class MoneyRow
    {
        public DateTime? Date { get; set; }
        public double Amount { get; set; }
        public string? Party { get; set; }
        public string? Number { get; set; }
    }

    // state of one request
    private CancellationToken _ct;
    private DateTime _now;
    private DateTime _today;
    private string? _uid;
    private readonly List<DashKpiDto> _kpis = new();
    private readonly List<DashChartDto> _charts = new();
    private readonly List<DashListDto> _lists = new();
    private List<ProductRow>? _products;
    private List<MoneyRow>? _sales;
    private double _salesPrev;
    private List<MoneyRow>? _purchases;
    private double _purchasesPrev;

    public static string PersonaOf(IEnumerable<string> roleList)
    {
        var roles = new HashSet<string>(roleList ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (roles.Contains("Administrator") || roles.Contains("Users")) return "admin";
        if (roles.Contains("Manager")) return "manager";
        if (roles.Contains("Auditor / Read-only")) return "auditor";
        if (roles.Contains("Finance / Accountant")) return "finance";
        if (roles.Contains("Warehouse Supervisor")) return "supervisor";
        if (roles.Contains("Warehouse Operator")) return "operator";
        if (roles.Contains("Quality / Returns Inspector")) return "quality";
        if (roles.Contains("Inventory Controller")) return "controller";
        if (roles.Contains("Buyer / Purchasing")) return "buyer";
        if (roles.Contains("Customer Service")) return "service";
        if (roles.Contains("Sales")) return "sales";
        if (roles.Contains("Warehouses")) return "operator";
        if (roles.Contains("Dashboards")) return "manager";
        if (roles.Contains("PurchaseOrders")) return "buyer";
        if (roles.Contains("SalesOrders")) return "sales";
        if (roles.Contains("Products")) return "controller";
        return "general";
    }

    // ---------- small helpers
    private static string Tone(double v, double good, double warn, bool higherBetter = true) =>
        higherBetter ? (v >= good ? "good" : v >= warn ? "warn" : "bad") : (v <= good ? "good" : v <= warn ? "warn" : "bad");

    private static string CountTone(double v, bool badWhenPositive = false) => v <= 0 ? "good" : badWhenPositive ? "bad" : "warn";

    private static string Dt(DateTime? d) => d == null ? "-" : d.Value.ToString("dd MMM");
    private static string Num(double v) => v.ToString("#,##0.##");
    private List<DateTime> Range(int n) => Enumerable.Range(0, n).Select(i => _today.AddDays(-(n - 1) + i)).ToList();

    private static string Delta(double cur, double prev)
    {
        if (prev <= 0) return cur > 0 ? "New activity in this period" : "No activity yet";
        var p = (cur - prev) / prev * 100;
        return (p >= 0 ? "+" : "") + Math.Round(p, 0) + "% vs previous 30 days";
    }

    private void Kpi(string label, double? value, string unit, string? note = null, string? tone = null, string? link = null, List<double>? spark = null) =>
        _kpis.Add(new DashKpiDto { Label = label, Value = value, Unit = unit, Note = note, Tone = tone, Link = link, Spark = spark ?? new List<double>() });

    private void Chart(string title, string type, List<string> labels, string unit, string? link, params (string Name, List<double> Values)[] series) =>
        _charts.Add(new DashChartDto
        {
            Key = title,
            Title = title,
            Type = type,
            Unit = unit,
            Link = link,
            Labels = labels,
            Series = series.Select(s => new DashSeriesDto { Name = s.Name, Values = s.Values }).ToList()
        });

    private void AddList(string title, string? link, string empty, string[] columns, IEnumerable<DashRowDto> rows) =>
        _lists.Add(new DashListDto { Key = title, Title = title, Link = link, Empty = empty, Columns = columns.ToList(), Rows = rows.ToList() });

    private static DashRowDto Row(string? link, string? tone, params string[] cells) =>
        new DashRowDto { Link = link, Tone = tone, Cells = cells.ToList() };

    // ---------- loaders
    private async Task<List<ProductRow>> LoadProducts()
    {
        if (_products != null) return _products;
        var rows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false)
            .GroupBy(x => x.ProductId).Select(g => new { Id = g.Key, Stock = g.Sum(x => x.Stock) }).ToListAsync(_ct);
        var stock = rows.Where(x => x.Id != null).ToDictionary(x => x.Id!, x => x.Stock ?? 0.0);
        _products = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.Physical == true)
            .Select(p => new ProductRow { Id = p.Id, Name = p.Name, Reorder = p.ReorderPoint ?? 0, ReorderQty = p.ReorderQuantity ?? 0, Price = p.UnitPrice ?? 0 })
            .ToListAsync(_ct);
        foreach (var p in _products) p.Qty = stock.TryGetValue(p.Id, out var q) ? q : 0;
        return _products;
    }

    private async Task LoadSales()
    {
        if (_sales != null) return;
        var since = _today.AddDays(-29);
        var prev = _today.AddDays(-59);
        _sales = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => (s.OrderStatus == SalesOrderStatus.Confirmed || s.OrderStatus == SalesOrderStatus.Archived) && s.OrderDate >= since)
            .Select(s => new MoneyRow { Date = s.OrderDate, Amount = s.AfterTaxAmount ?? 0, Party = s.Customer!.Name, Number = s.Number }).ToListAsync(_ct);
        _salesPrev = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => (s.OrderStatus == SalesOrderStatus.Confirmed || s.OrderStatus == SalesOrderStatus.Archived) && s.OrderDate >= prev && s.OrderDate < since)
            .SumAsync(s => (double?)s.AfterTaxAmount, _ct) ?? 0;
    }

    private async Task LoadPurchases()
    {
        if (_purchases != null) return;
        var since = _today.AddDays(-29);
        var prev = _today.AddDays(-59);
        _purchases = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => (s.OrderStatus == PurchaseOrderStatus.Confirmed || s.OrderStatus == PurchaseOrderStatus.Archived) && s.OrderDate >= since)
            .Select(s => new MoneyRow { Date = s.OrderDate, Amount = s.AfterTaxAmount ?? 0, Party = s.Vendor!.Name, Number = s.Number }).ToListAsync(_ct);
        _purchasesPrev = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => (s.OrderStatus == PurchaseOrderStatus.Confirmed || s.OrderStatus == PurchaseOrderStatus.Archived) && s.OrderDate >= prev && s.OrderDate < since)
            .SumAsync(s => (double?)s.AfterTaxAmount, _ct) ?? 0;
    }

    private static List<double> Daily(List<MoneyRow> rows, List<DateTime> days) =>
        days.Select(d => Math.Round(rows.Where(r => r.Date != null && r.Date.Value.Date == d).Sum(r => r.Amount), 2)).ToList();

    private static (List<string> Labels, List<double> Values) Top(List<MoneyRow> rows, int n = 6)
    {
        var top = rows.GroupBy(r => r.Party ?? "-").Select(g => new { Name = g.Key, Sum = g.Sum(x => x.Amount) }).OrderByDescending(x => x.Sum).Take(n).ToList();
        return (top.Select(x => x.Name).ToList(), top.Select(x => Math.Round(x.Sum, 2)).ToList());
    }

    // ---------- blocks
    private async Task SalesBlock(bool kpis, bool charts, bool topCustomers)
    {
        await LoadSales();
        var days = Range(30);
        var daily = Daily(_sales!, days);
        var total = _sales!.Sum(r => r.Amount);
        if (kpis)
        {
            Kpi("Revenue, last 30 days", Math.Round(total, 0), "money", Delta(total, _salesPrev), total >= _salesPrev ? "good" : "warn", "/SalesReports/SalesReportList", daily);
            Kpi("Orders, last 30 days", _sales.Count, "", "Confirmed sales orders", null, "/SalesOrders/SalesOrderList", days.Select(d => (double)_sales.Count(r => r.Date != null && r.Date.Value.Date == d)).ToList());
            Kpi("Average order value", _sales.Count > 0 ? Math.Round(total / _sales.Count, 0) : null, "money", "Revenue divided by orders", null, "/SalesReports/SalesReportList");
        }
        if (charts) Chart("Revenue per day", "line", days.Select(d => d.ToString("dd MMM")).ToList(), "money", "/SalesReports/SalesReportList", ("Revenue", daily));
        if (topCustomers)
        {
            var t = Top(_sales!);
            Chart("Top customers, 30 days", "hbar", t.Labels, "money", "/Customers/CustomerList", ("Revenue", t.Values));
        }
    }

    private async Task PurchaseBlock(bool kpis, bool charts, bool topVendors)
    {
        await LoadPurchases();
        var days = Range(30);
        var daily = Daily(_purchases!, days);
        var total = _purchases!.Sum(r => r.Amount);
        if (kpis)
            Kpi("Purchases, last 30 days", Math.Round(total, 0), "money", Delta(total, _purchasesPrev), null, "/PurchaseReports/PurchaseReportList", daily);
        if (charts) Chart("Purchases per day", "line", days.Select(d => d.ToString("dd MMM")).ToList(), "money", "/PurchaseReports/PurchaseReportList", ("Purchases", daily));
        if (topVendors)
        {
            var t = Top(_purchases!);
            Chart("Top vendors, 30 days", "hbar", t.Labels, "money", "/Vendors/VendorList", ("Spend", t.Values));
        }
    }

    private async Task RevenueVsPurchases()
    {
        await LoadSales(); await LoadPurchases();
        var days = Range(30);
        Chart("Revenue and purchases", "line", days.Select(d => d.ToString("dd MMM")).ToList(), "money", "/Dashboards/BusinessAnalytics",
            ("Revenue", Daily(_sales!, days)), ("Purchases", Daily(_purchases!, days)));
    }

    private async Task FlowChart()
    {
        var days = Range(14);
        var since = days[0];
        var mv = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.MovementDate >= since && x.Warehouse!.SystemWarehouse == false)
            .Select(x => new { x.MovementDate, x.Movement }).ToListAsync(_ct);
        var ins = days.Select(d => Math.Round(mv.Where(m => m.MovementDate != null && m.MovementDate.Value.Date == d && (m.Movement ?? 0) > 0).Sum(m => m.Movement ?? 0), 2)).ToList();
        var outs = days.Select(d => Math.Round(-mv.Where(m => m.MovementDate != null && m.MovementDate.Value.Date == d && (m.Movement ?? 0) < 0).Sum(m => m.Movement ?? 0), 2)).ToList();
        Chart("Units in and out, 14 days", "bar", days.Select(d => d.ToString("dd MMM")).ToList(), "", "/TransactionReports/TransactionReportList", ("In", ins), ("Out", outs));
    }

    private async Task StockByWarehouse()
    {
        var wh = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false)
            .GroupBy(x => x.Warehouse!.Name).Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Stock) }).ToListAsync(_ct);
        var top = wh.OrderByDescending(x => x.Qty).Take(8).ToList();
        Chart("Units on hand by warehouse", "hbar", top.Select(x => x.Name ?? "-").ToList(), "", "/StockReports/StockReportList", ("Units", top.Select(x => Math.Round(x.Qty ?? 0, 2)).ToList()));
    }

    private async Task StockKpis(bool value, bool low)
    {
        var products = await LoadProducts();
        var below = products.Where(p => p.Reorder > 0 && p.Qty <= p.Reorder).ToList();
        if (value) Kpi("Stock value", Math.Round(products.Sum(p => Math.Max(p.Qty, 0) * p.Price), 0), "money", "On-hand units at unit price", null, "/StockReports/Costing");
        if (low) Kpi("Items at or below reorder point", below.Count, "", below.Count == 0 ? "Everything is above its reorder point" : "Order these soon", CountTone(below.Count), "/PurchaseOrders/ReorderSuggestions");
    }

    private async Task LowStockList(bool withSuggestion)
    {
        var products = await LoadProducts();
        var below = products.Where(p => p.Reorder > 0 && p.Qty <= p.Reorder).OrderBy(p => p.Qty / p.Reorder).Take(8).ToList();
        if (withSuggestion)
            AddList("Reorder these first", "/PurchaseOrders/ReorderSuggestions", "No item is below its reorder point.", new[] { "Product", "On hand", "Reorder point", "Suggested order" },
                below.Select(p => Row("/PurchaseOrders/ReorderSuggestions", p.Qty <= 0 ? "bad" : "warn", p.Name ?? "-", Num(p.Qty), Num(p.Reorder), Num(p.ReorderQty))));
        else
            AddList("Low stock", "/Products/StockAvailability", "No item is below its reorder point.", new[] { "Product", "On hand", "Reorder point" },
                below.Select(p => Row("/Products/StockAvailability", p.Qty <= 0 ? "bad" : "warn", p.Name ?? "-", Num(p.Qty), Num(p.Reorder))));
    }

    private async Task ExpiringBatches(bool kpi)
    {
        var soon = _today.AddDays(30);
        var rows = await _context.ProductBatch.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(b => b.ExpiryDate != null && b.ExpiryDate <= soon && (b.Quantity ?? 0) > 0)
            .OrderBy(b => b.ExpiryDate).Take(8)
            .Select(b => new { Product = b.Product!.Name, b.BatchNumber, b.ExpiryDate, b.Quantity }).ToListAsync(_ct);
        var count = await _context.ProductBatch.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(b => b.ExpiryDate != null && b.ExpiryDate <= soon && (b.Quantity ?? 0) > 0, _ct);
        if (kpi) Kpi("Batches expiring in 30 days", count, "", count == 0 ? "Nothing close to expiry" : "Sell, use or move them", CountTone(count, true), "/Products/ProductBatches");
        AddList("Batches about to expire", "/Products/ProductBatches", "No batch expires in the next 30 days.", new[] { "Product", "Batch", "Expires", "Units" },
            rows.Select(b => Row("/Products/ProductBatches", b.ExpiryDate < _today ? "bad" : "warn", b.Product ?? "-", b.BatchNumber ?? "-", Dt(b.ExpiryDate), Num(b.Quantity ?? 0))));
    }

    private async Task DeliveriesList(string title)
    {
        var rows = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status == DeliveryOrderStatus.Draft || d.Status == DeliveryOrderStatus.Confirmed)
            .OrderBy(d => d.DeliveryDate).Take(8)
            .Select(d => new { d.Number, d.DeliveryDate, d.Status, Customer = d.SalesOrder!.Customer!.Name }).ToListAsync(_ct);
        AddList(title, "/DeliveryOrders/DeliveryOrderList", "No delivery is waiting.", new[] { "Delivery", "Customer", "Date", "Status" },
            rows.Select(d => Row("/DeliveryOrders/DeliveryOrderList", d.DeliveryDate < _today ? "bad" : null, d.Number ?? "-", d.Customer ?? "-", Dt(d.DeliveryDate), d.Status.ToString() ?? "-")));
    }

    private async Task PurchaseOrdersAwaiting()
    {
        var confirmed = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.OrderStatus == PurchaseOrderStatus.Confirmed)
            .Select(p => new { p.Id, p.Number, p.OrderDate, p.AfterTaxAmount, Vendor = p.Vendor!.Name }).ToListAsync(_ct);
        var ids = confirmed.Select(c => c.Id).ToList();
        var received = (await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(g => g.Status != GoodsReceiveStatus.Cancelled && ids.Contains(g.PurchaseOrderId!)).Select(g => g.PurchaseOrderId).Distinct().ToListAsync(_ct)).ToHashSet();
        var waiting = confirmed.Where(c => !received.Contains(c.Id)).OrderBy(c => c.OrderDate).ToList();
        AddList("Orders waiting for goods", "/GoodsReceives/GoodsReceiveList", "Every confirmed order has been received.", new[] { "Order", "Vendor", "Ordered", "Amount" },
            waiting.Take(8).Select(c => Row("/GoodsReceives/GoodsReceiveList", c.OrderDate < _today.AddDays(-14) ? "warn" : null, c.Number ?? "-", c.Vendor ?? "-", Dt(c.OrderDate), Num(c.AfterTaxAmount ?? 0))));
        Kpi("Confirmed, nothing received", waiting.Count, "", waiting.Count == 0 ? "No open supplier orders" : "Chase these suppliers", CountTone(waiting.Count), "/GoodsReceives/GoodsReceiveList");
        Kpi("Open order value", Math.Round(waiting.Sum(c => c.AfterTaxAmount ?? 0), 0), "money", "Confirmed orders not yet received", null, "/PurchaseOrders/PurchaseOrderList");
    }

    private async Task OrdersByStatus(bool sales)
    {
        List<(string, double)> rows;
        if (sales)
        {
            var g = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).GroupBy(s => s.OrderStatus).Select(x => new { S = x.Key, C = x.Count() }).ToListAsync(_ct);
            rows = g.Select(x => (x.S.ToString() ?? "-", (double)x.C)).ToList();
        }
        else
        {
            var g = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false).GroupBy(s => s.OrderStatus).Select(x => new { S = x.Key, C = x.Count() }).ToListAsync(_ct);
            rows = g.Select(x => (x.S.ToString() ?? "-", (double)x.C)).ToList();
        }
        Chart(sales ? "Sales orders by status" : "Purchase orders by status", "donut", rows.Select(r => r.Item1).ToList(), "", sales ? "/SalesOrders/SalesOrderList" : "/PurchaseOrders/PurchaseOrderList", ("Orders", rows.Select(r => r.Item2).ToList()));
    }

    private async Task DraftCounts(bool sales, string kpiLabel)
    {
        var drafts = sales
            ? await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.OrderStatus == SalesOrderStatus.Draft, _ct)
            : await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.OrderStatus == PurchaseOrderStatus.Draft, _ct);
        Kpi(kpiLabel, drafts, "", drafts == 0 ? "Nothing waiting" : "Review and confirm", CountTone(drafts), sales ? "/SalesOrders/SalesOrderList" : "/PurchaseOrders/PurchaseOrderList");
    }

    private async Task SalesWaitingDelivery()
    {
        var confirmed = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.OrderStatus == SalesOrderStatus.Confirmed)
            .Select(s => new { s.Id, s.Number, s.OrderDate, s.AfterTaxAmount, Customer = s.Customer!.Name }).ToListAsync(_ct);
        var ids = confirmed.Select(c => c.Id).ToList();
        var delivered = (await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status != DeliveryOrderStatus.Cancelled && ids.Contains(d.SalesOrderId!)).Select(d => d.SalesOrderId).Distinct().ToListAsync(_ct)).ToHashSet();
        var waiting = confirmed.Where(c => !delivered.Contains(c.Id)).OrderBy(c => c.OrderDate).ToList();
        Kpi("Confirmed, no delivery yet", waiting.Count, "", waiting.Count == 0 ? "Every order has a delivery" : "Create the deliveries", CountTone(waiting.Count), "/DeliveryOrders/DeliveryOrderList");
        AddList("Orders waiting for delivery", "/DeliveryOrders/DeliveryOrderList", "Every confirmed order already has a delivery.", new[] { "Order", "Customer", "Ordered", "Amount" },
            waiting.Take(8).Select(c => Row("/DeliveryOrders/DeliveryOrderList", c.OrderDate < _today.AddDays(-3) ? "warn" : null, c.Number ?? "-", c.Customer ?? "-", Dt(c.OrderDate), Num(c.AfterTaxAmount ?? 0))));
    }

    private async Task FloorKpis()
    {
        var uid = _uid;
        var tasks = await _context.WarehouseTask.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(t => (t.Status == "Open" || t.Status == "InProgress") && (t.AssignedToId == uid || t.AssignedToId == null)).ToListAsync(_ct);
        var overdue = tasks.Count(t => t.DueAtUtc != null && t.DueAtUtc < _now);
        var toPick = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(d => d.Status == DeliveryOrderStatus.Draft || d.Status == DeliveryOrderStatus.Confirmed, _ct);
        var waves = await _context.PickWave.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(w => w.Status == "Open", _ct);
        var packing = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.Status == "Packing", _ct);
        Kpi("My open tasks", tasks.Count, "", tasks.Count == 0 ? "You are all caught up" : "Assigned to you or open to anyone", tasks.Count == 0 ? "good" : null, "/Warehouses/Tasks");
        Kpi("Overdue tasks", overdue, "", overdue == 0 ? "Nothing is late" : "Do these first", CountTone(overdue, true), "/Warehouses/Tasks");
        Kpi("Orders to pick or ship", toPick, "", null, null, "/Warehouses/PickList");
        Kpi("Open pick waves", waves, "", null, null, "/Warehouses/WavePicking");
        Kpi("Shipments being packed", packing, "", null, null, "/Warehouses/Packing");

        var rows = tasks.OrderBy(t => t.DueAtUtc ?? DateTime.MaxValue).ThenBy(t => t.Priority == "High" ? 0 : t.Priority == "Normal" ? 1 : 2).Take(8).ToList();
        AddList("My task list", "/Warehouses/Tasks", "No open tasks.", new[] { "Task", "Type", "Priority", "Due" },
            rows.Select(t => Row("/Warehouses/Tasks", t.DueAtUtc != null && t.DueAtUtc < _now ? "bad" : t.Priority == "High" ? "warn" : null, t.Title ?? "-", t.TaskType ?? "-", t.Priority ?? "-", Dt(t.DueAtUtc))));

        var byStatus = await _context.WarehouseTask.AsNoTracking().ApplyIsDeletedFilter(false).GroupBy(t => t.Status).Select(g => new { S = g.Key, C = g.Count() }).ToListAsync(_ct);
        Chart("Tasks by status", "donut", byStatus.Select(x => x.S ?? "-").ToList(), "", "/Warehouses/Tasks", ("Tasks", byStatus.Select(x => (double)x.C).ToList()));
    }

    private async Task DockToday(bool kpis)
    {
        var start = _today; var end = _today.AddDays(1);
        var appts = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false).Where(a => a.StartAtUtc >= start && a.StartAtUtc < end).OrderBy(a => a.StartAtUtc).ToListAsync(_ct);
        var late = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(a => a.Status == "Scheduled" && a.StartAtUtc < _now.AddMinutes(-15), _ct);
        if (kpis)
        {
            Kpi("Trucks today", appts.Count(a => a.Status != "Cancelled"), "", $"{appts.Count(a => a.Status == "CheckedIn")} on site now", null, "/Warehouses/DockSchedule");
            Kpi("Trucks late right now", late, "", late == 0 ? "Everyone is on time" : "Scheduled, over 15 minutes past slot", CountTone(late, true), "/Warehouses/DockSchedule");
        }
        AddList("Dock schedule today", "/Warehouses/DockSchedule", "No trucks booked for today.", new[] { "Time", "Direction", "Carrier", "Status" },
            appts.Where(a => a.Status != "Cancelled").Take(8).Select(a => Row("/Warehouses/DockSchedule",
                a.Status == "Scheduled" && a.StartAtUtc < _now.AddMinutes(-15) ? "bad" : null,
                a.StartAtUtc == null ? "-" : a.StartAtUtc.Value.ToString("HH:mm"), a.Direction ?? "-", a.Carrier ?? a.PartyName ?? "-", a.Status ?? "-")));
    }

    private async Task HoldsBlock(bool kpi, bool list, bool chart)
    {
        var holds = await _context.StockHold.AsNoTracking().ApplyIsDeletedFilter(false).Where(h => h.Status == "Active").ToListAsync(_ct);
        if (kpi) Kpi("Units on hold", holds.Sum(h => h.Quantity ?? 0), "", holds.Count == 0 ? "No stock is held" : $"{holds.Count} active holds", holds.Count > 0 ? "warn" : "good", "/Warehouses/StockHolds");
        if (chart)
        {
            var byReason = holds.GroupBy(h => h.Reason ?? "Other").Select(g => new { R = g.Key, Q = g.Sum(h => h.Quantity ?? 0) }).ToList();
            Chart("Held units by reason", "donut", byReason.Select(x => x.R).ToList(), "", "/Warehouses/StockHolds", ("Units", byReason.Select(x => Math.Round(x.Q, 2)).ToList()));
        }
        if (list)
        {
            var ids = holds.Select(h => h.ProductId).Distinct().ToList();
            var names = await _context.Product.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync(_ct);
            var map = names.ToDictionary(n => n.Id, n => n.Name ?? "-");
            AddList("Stock on hold", "/Warehouses/StockHolds", "No active holds.", new[] { "Product", "Units", "Reason" },
                holds.OrderByDescending(h => h.Quantity).Take(8).Select(h => Row("/Warehouses/StockHolds", "warn", h.ProductId != null && map.ContainsKey(h.ProductId) ? map[h.ProductId] : "-", Num(h.Quantity ?? 0), h.Reason ?? "-")));
        }
    }

    private async Task ShipmentsChart()
    {
        var days = Range(14);
        var since = days[0];
        var shipped = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.Status == "Shipped" && s.ShippedAtUtc >= since).Select(s => s.ShippedAtUtc).ToListAsync(_ct);
        var grs = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(g => (g.Status == GoodsReceiveStatus.Confirmed || g.Status == GoodsReceiveStatus.Archived) && g.ReceiveDate >= since).Select(g => g.ReceiveDate).ToListAsync(_ct);
        Chart("Shipments and receipts, 14 days", "bar", days.Select(d => d.ToString("dd MMM")).ToList(), "", "/Warehouses/Kpis",
            ("Shipments", days.Select(d => (double)shipped.Count(s => s != null && s.Value.Date == d)).ToList()),
            ("Receipts", days.Select(d => (double)grs.Count(s => s != null && s.Value.Date == d)).ToList()));
    }

    private async Task CountsKpis()
    {
        var open = await _context.StockCount.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(c => c.Status == StockCountStatus.Draft, _ct);
        Kpi("Stock counts in progress", open, "", null, null, "/StockCounts/StockCountList");
        var since = _today.AddDays(-29);
        var counts = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.MovementDate >= since && x.ModuleName == "StockCount")
            .Select(x => new { Sys = x.QtySCSys, Delta = x.QtySCDelta }).ToListAsync(_ct);
        var sys = counts.Sum(c => Math.Abs(c.Sys ?? 0)); var delta = counts.Sum(c => Math.Abs(c.Delta ?? 0));
        double? acc = sys > 0 ? Math.Round(100 * (1 - Math.Min(delta / sys, 1)), 1) : null;
        Kpi("Inventory accuracy", acc, "%", "Counted against system quantity, 30 days", acc == null ? null : Tone(acc.Value, 98, 95), "/StockCounts/CycleCounts");
    }

    private async Task AdminBlock()
    {
        var pending = await _context.ApprovalRequest.AsNoTracking().ApplyIsDeletedFilter(false).Where(a => a.Status == "Pending").OrderBy(a => a.CreatedAtUtc).ToListAsync(_ct);
        var failing = await _context.WebhookEndpoint.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(h => h.IsActive == true && h.LastStatus != null && h.LastStatus.StartsWith("Failed"), _ct);
        var replies = await _context.PortalResponse.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(p => p.CreatedAtUtc >= _today.AddDays(-7), _ct);
        var since = _today.AddDays(-13);
        var audit = await _context.AuditLog.AsNoTracking().ApplyIsDeletedFilter(false).Where(a => a.CreatedAtUtc >= since).Select(a => new { a.CreatedAtUtc, a.Action, a.EntityName }).ToListAsync(_ct);
        var days = Range(14);
        var perDay = days.Select(d => (double)audit.Count(a => a.CreatedAtUtc != null && a.CreatedAtUtc.Value.Date == d)).ToList();

        Kpi("Approvals waiting", pending.Count, "", pending.Count == 0 ? "No decisions needed" : "Someone is waiting on you", CountTone(pending.Count), "/Users/Approvals");
        Kpi("Failing webhooks", failing, "", failing == 0 ? "All integrations delivering" : "Check the endpoints", CountTone(failing, true), "/Users/Integrations");
        Kpi("Changes today", (double)perDay.Last(), "", "Records created, edited or deleted", null, "/Users/AuditLog", perDay);
        Kpi("Partner replies, 7 days", replies, "", "Suppliers answering through portals", null, "/PurchaseOrders/Portal");

        Chart("Activity per day, 14 days", "bar", days.Select(d => d.ToString("dd MMM")).ToList(), "", "/Users/AuditLog",
            ("Created", days.Select(d => (double)audit.Count(a => a.CreatedAtUtc != null && a.CreatedAtUtc.Value.Date == d && a.Action == "Created")).ToList()),
            ("Updated", days.Select(d => (double)audit.Count(a => a.CreatedAtUtc != null && a.CreatedAtUtc.Value.Date == d && a.Action == "Updated")).ToList()),
            ("Deleted", days.Select(d => (double)audit.Count(a => a.CreatedAtUtc != null && a.CreatedAtUtc.Value.Date == d && a.Action == "Deleted")).ToList()));
        var byEntity = audit.GroupBy(a => a.EntityName ?? "-").Select(g => new { N = g.Key, C = g.Count() }).OrderByDescending(x => x.C).Take(8).ToList();
        Chart("Most changed records, 14 days", "hbar", byEntity.Select(x => x.N).ToList(), "", "/Users/AuditLog", ("Changes", byEntity.Select(x => (double)x.C).ToList()));

        AddList("Waiting for approval", "/Users/Approvals", "Nothing is waiting for approval.", new[] { "Type", "Number", "Units", "Requested" },
            pending.Take(8).Select(a => Row("/Users/Approvals", "warn", a.EntityType ?? "-", a.EntityNumber ?? "-", Num(a.Units ?? 0), Dt(a.CreatedAtUtc))));
        var recent = await _context.AuditLog.AsNoTracking().ApplyIsDeletedFilter(false).OrderByDescending(a => a.CreatedAtUtc).Take(8)
            .Select(a => new { a.CreatedAtUtc, a.Action, a.EntityName, a.EntityLabel }).ToListAsync(_ct);
        AddList("Recent changes", "/Users/AuditLog", "No changes recorded yet.", new[] { "When", "Action", "Record", "Label" },
            recent.Select(a => Row("/Users/AuditLog", a.Action == "Deleted" ? "bad" : null, a.CreatedAtUtc == null ? "-" : a.CreatedAtUtc.Value.ToString("dd MMM HH:mm"), a.Action ?? "-", a.EntityName ?? "-", a.EntityLabel ?? "-")));
    }

    private async Task LargestOrders(bool sales)
    {
        if (sales) await LoadSales(); else await LoadPurchases();
        var src = sales ? _sales! : _purchases!;
        AddList(sales ? "Largest sales orders, 30 days" : "Largest purchase orders, 30 days", sales ? "/SalesReports/SalesReportList" : "/PurchaseReports/PurchaseReportList", "No orders in the last 30 days.",
            new[] { "Order", sales ? "Customer" : "Vendor", "Date", "Amount" },
            src.OrderByDescending(r => r.Amount).Take(8).Select(r => Row(null, null, r.Number ?? "-", r.Party ?? "-", Dt(r.Date), Num(r.Amount))));
    }

    private async Task RecentSales()
    {
        var rows = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).OrderByDescending(s => s.OrderDate).Take(8)
            .Select(s => new { s.Number, s.OrderDate, s.OrderStatus, s.AfterTaxAmount, Customer = s.Customer!.Name }).ToListAsync(_ct);
        AddList("Latest sales orders", "/SalesOrders/SalesOrderList", "No sales orders yet.", new[] { "Order", "Customer", "Date", "Status", "Amount" },
            rows.Select(s => Row("/SalesOrders/SalesOrderList", s.OrderStatus == SalesOrderStatus.Draft ? "warn" : null, s.Number ?? "-", s.Customer ?? "-", Dt(s.OrderDate), s.OrderStatus.ToString() ?? "-", Num(s.AfterTaxAmount ?? 0))));
    }

    private async Task DraftPurchaseList()
    {
        var rows = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.OrderStatus == PurchaseOrderStatus.Draft).OrderBy(p => p.OrderDate).Take(8)
            .Select(p => new { p.Number, p.OrderDate, p.AfterTaxAmount, Vendor = p.Vendor!.Name }).ToListAsync(_ct);
        AddList("Draft purchase orders", "/PurchaseOrders/PurchaseOrderList", "No draft orders.", new[] { "Order", "Vendor", "Date", "Amount" },
            rows.Select(p => Row("/PurchaseOrders/PurchaseOrderList", "warn", p.Number ?? "-", p.Vendor ?? "-", Dt(p.OrderDate), Num(p.AfterTaxAmount ?? 0))));
    }

    private async Task DraftSalesList()
    {
        var rows = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.OrderStatus == SalesOrderStatus.Draft).OrderBy(p => p.OrderDate).Take(8)
            .Select(p => new { p.Number, p.OrderDate, p.AfterTaxAmount, Customer = p.Customer!.Name }).ToListAsync(_ct);
        AddList("Draft orders to confirm", "/SalesOrders/SalesOrderList", "No draft orders.", new[] { "Order", "Customer", "Date", "Amount" },
            rows.Select(p => Row("/SalesOrders/SalesOrderList", "warn", p.Number ?? "-", p.Customer ?? "-", Dt(p.OrderDate), Num(p.AfterTaxAmount ?? 0))));
    }

    // ---------- shared helpers for the newer roles
    private async Task<Dictionary<string, string>> ProductNames(IEnumerable<string?> ids)
    {
        var list = ids.Where(i => i != null).Select(i => i!).Distinct().ToList();
        var rows = await _context.Product.AsNoTracking().Where(p => list.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync(_ct);
        return rows.ToDictionary(r => r.Id, r => r.Name ?? "-");
    }

    private async Task PackToShipKpi()
    {
        var since = _today.AddDays(-29);
        var shipped = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.Status == "Shipped" && s.ShippedAtUtc >= since)
            .Select(s => new { s.CreatedAtUtc, s.ShippedAtUtc }).ToListAsync(_ct);
        var hours = shipped.Where(s => s.CreatedAtUtc != null && s.ShippedAtUtc != null).Select(s => (s.ShippedAtUtc!.Value - s.CreatedAtUtc!.Value).TotalHours).ToList();
        Kpi("Pack-to-ship time", hours.Count > 0 ? Math.Round(hours.Average(), 1) : null, "hours", "Average over 30 days, lower is better", hours.Count > 0 ? Tone(hours.Average(), 8, 24, false) : null, "/Warehouses/Kpis");
    }

    private async Task DockOnTimeKpi()
    {
        var since = _today.AddDays(-29);
        var arrived = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(a => a.StartAtUtc >= since && a.StartAtUtc < _today.AddDays(1) && a.CheckedInAtUtc != null).ToListAsync(_ct);
        double? onTime = arrived.Count > 0 ? Math.Round(100.0 * arrived.Count(a => a.CheckedInAtUtc <= a.StartAtUtc!.Value.AddMinutes(15)) / arrived.Count, 0) : null;
        Kpi("Trucks on time", onTime, "%", "Arrived within 15 minutes of the slot, 30 days", onTime == null ? null : Tone(onTime.Value, 90, 75), "/Warehouses/Kpis");
    }

    private async Task LateDeliveries(bool kpi)
    {
        var rows = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => (d.Status == DeliveryOrderStatus.Draft || d.Status == DeliveryOrderStatus.Confirmed) && d.DeliveryDate < _today)
            .OrderBy(d => d.DeliveryDate).Take(8)
            .Select(d => new { d.Number, d.DeliveryDate, Customer = d.SalesOrder!.Customer!.Name }).ToListAsync(_ct);
        var count = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .CountAsync(d => (d.Status == DeliveryOrderStatus.Draft || d.Status == DeliveryOrderStatus.Confirmed) && d.DeliveryDate < _today, _ct);
        if (kpi) Kpi("Late deliveries", count, "", count == 0 ? "Nothing is past its date" : "Past the promised delivery date", CountTone(count, true), "/DeliveryOrders/DeliveryOrderList");
        AddList("Late deliveries", "/DeliveryOrders/DeliveryOrderList", "No delivery is past its date.", new[] { "Delivery", "Customer", "Was due", "Days late" },
            rows.Select(d => Row("/DeliveryOrders/DeliveryOrderList", "bad", d.Number ?? "-", d.Customer ?? "-", Dt(d.DeliveryDate), d.DeliveryDate == null ? "-" : ((int)(_today - d.DeliveryDate.Value.Date).TotalDays).ToString())));
    }

    private async Task OrderCycleKpi()
    {
        var since = _today.AddDays(-29);
        var shipped = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.Status == "Shipped" && s.ShippedAtUtc >= since)
            .Select(s => new { s.DeliveryOrderId, s.ShippedAtUtc }).ToListAsync(_ct);
        var ids = shipped.Select(s => s.DeliveryOrderId).Distinct().ToList();
        var doSo = await _context.DeliveryOrder.AsNoTracking().Where(d => ids.Contains(d.Id)).Select(d => new { d.Id, OrderDate = d.SalesOrder!.OrderDate }).ToListAsync(_ct);
        var soDate = doSo.Where(d => d.OrderDate != null).ToDictionary(d => d.Id, d => d.OrderDate!.Value);
        var cycle = shipped.Where(s => s.DeliveryOrderId != null && soDate.ContainsKey(s.DeliveryOrderId) && s.ShippedAtUtc != null)
            .Select(s => (s.ShippedAtUtc!.Value.Date - soDate[s.DeliveryOrderId!].Date).TotalDays).Where(d => d >= 0).ToList();
        Kpi("Order cycle time", cycle.Count > 0 ? Math.Round(cycle.Average(), 1) : null, "days", "Sales order date to shipment, 30 days", cycle.Count > 0 ? Tone(cycle.Average(), 2, 5, false) : null, "/Warehouses/Kpis");
    }

    private async Task ReturnRateKpi()
    {
        var since = _today.AddDays(-29);
        var moves = _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false).Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.MovementDate >= since);
        var outQty = Math.Abs(await moves.Where(x => x.ModuleName == "DeliveryOrder").SumAsync(x => (double?)x.Movement, _ct) ?? 0);
        var retQty = Math.Abs(await moves.Where(x => x.ModuleName == "SalesReturn").SumAsync(x => (double?)x.Movement, _ct) ?? 0);
        double? rate = outQty > 0 ? Math.Round(100 * retQty / outQty, 1) : null;
        Kpi("Customer return rate", rate, "%", "Units returned against units delivered, 30 days", rate == null ? null : Tone(rate.Value, 2, 5, false), "/SalesReturns/SalesReturnList");
    }

    private async Task Supervisor()
    {
        var tasks = await _context.WarehouseTask.AsNoTracking().ApplyIsDeletedFilter(false).Where(t => t.Status == "Open" || t.Status == "InProgress").ToListAsync(_ct);
        var overdue = tasks.Where(t => t.DueAtUtc != null && t.DueAtUtc < _now).ToList();
        var unassigned = tasks.Where(t => t.AssignedToId == null).ToList();
        var toPick = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(d => d.Status == DeliveryOrderStatus.Draft || d.Status == DeliveryOrderStatus.Confirmed, _ct);
        var packing = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.Status == "Packing", _ct);

        await LateDeliveries(true);
        Kpi("Overdue tasks", overdue.Count, "", overdue.Count == 0 ? "Nothing is late" : "Past their due time", CountTone(overdue.Count, true), "/Warehouses/Tasks");
        Kpi("Tasks nobody owns", unassigned.Count, "", unassigned.Count == 0 ? "Everything is assigned" : "Assign them to someone", CountTone(unassigned.Count), "/Warehouses/Tasks");
        Kpi("Open tasks", tasks.Count, "", "Open or in progress", null, "/Warehouses/Tasks");
        Kpi("Orders to pick or ship", toPick, "", null, null, "/Warehouses/PickList");
        Kpi("Shipments being packed", packing, "", null, null, "/Warehouses/Packing");
        await DockToday(true);
        await PackToShipKpi();
        await DockOnTimeKpi();
        await CountsKpis();

        var attention = overdue.Concat(unassigned).Distinct().OrderBy(t => t.DueAtUtc ?? DateTime.MaxValue).Take(8).ToList();
        AddList("Overdue and unassigned tasks", "/Warehouses/Tasks", "No overdue or unassigned tasks.", new[] { "Task", "Type", "Owner", "Due" },
            attention.Select(t => Row("/Warehouses/Tasks", t.DueAtUtc != null && t.DueAtUtc < _now ? "bad" : "warn", t.Title ?? "-", t.TaskType ?? "-", t.AssignedToId == null ? "Unassigned" : "Assigned", Dt(t.DueAtUtc))));
        await DeliveriesList("Deliveries waiting");

        var days = Range(14);
        var since = days[0];
        var done = await _context.WarehouseTask.AsNoTracking().ApplyIsDeletedFilter(false).Where(t => t.CompletedAtUtc >= since).Select(t => t.CompletedAtUtc).ToListAsync(_ct);
        Chart("Tasks completed per day, 14 days", "bar", days.Select(d => d.ToString("dd MMM")).ToList(), "", "/Warehouses/Tasks",
            ("Completed", days.Select(d => (double)done.Count(x => x != null && x.Value.Date == d)).ToList()));
        await ShipmentsChart();
        await FlowChart();
        var byStatus = await _context.WarehouseTask.AsNoTracking().ApplyIsDeletedFilter(false).GroupBy(t => t.Status).Select(g => new { S = g.Key, C = g.Count() }).ToListAsync(_ct);
        Chart("Tasks by status", "donut", byStatus.Select(x => x.S ?? "-").ToList(), "", "/Warehouses/Tasks", ("Tasks", byStatus.Select(x => (double)x.C).ToList()));
    }

    private async Task Quality()
    {
        var since = _today.AddDays(-29);
        var insp = await _context.ReturnInspection.AsNoTracking().ApplyIsDeletedFilter(false).Where(i => i.CreatedAtUtc >= since)
            .Select(i => new { i.CreatedAtUtc, i.ProductId, i.ReasonCode, Returned = i.ReturnedQuantity ?? 0, Good = i.GoodQuantity ?? 0, Damaged = i.DamagedQuantity ?? 0 }).ToListAsync(_ct);
        var inspectedIds = (await _context.ReturnInspection.AsNoTracking().ApplyIsDeletedFilter(false).Select(i => i.SalesReturnId).Distinct().ToListAsync(_ct)).Where(x => x != null).ToHashSet();
        var recent = _today.AddDays(-59);
        var returns = await _context.SalesReturn.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(r => r.Status != SalesReturnStatus.Cancelled && r.ReturnDate >= recent)
            .OrderBy(r => r.ReturnDate).Select(r => new { r.Id, r.Number, r.ReturnDate, Customer = r.DeliveryOrder!.SalesOrder!.Customer!.Name }).ToListAsync(_ct);
        var waiting = returns.Where(r => !inspectedIds.Contains(r.Id)).ToList();

        var returned = insp.Sum(i => i.Returned); var damaged = insp.Sum(i => i.Damaged); var good = insp.Sum(i => i.Good);
        Kpi("Returns waiting for inspection", waiting.Count, "", waiting.Count == 0 ? "Every return has been inspected" : "Customers are waiting on a decision", CountTone(waiting.Count), "/Warehouses/ReturnsInspection");
        await HoldsBlock(true, false, false);
        double? dmgRate = returned > 0 ? Math.Round(100 * damaged / returned, 1) : null;
        Kpi("Damaged share of returns", dmgRate, "%", "Damaged units against returned units, 30 days", dmgRate == null ? null : Tone(dmgRate.Value, 10, 25, false), "/Warehouses/ReturnsInspection");
        Kpi("Units back to stock", Math.Round(good, 0), "", "Inspected as good, 30 days", null, "/Warehouses/ReturnsInspection");
        await ReturnRateKpi();
        await ExpiringBatches(true);

        AddList("Returns waiting for inspection", "/Warehouses/ReturnsInspection", "Every return has been inspected.", new[] { "Return", "Customer", "Date" },
            waiting.Take(8).Select(r => Row("/Warehouses/ReturnsInspection", "warn", r.Number ?? "-", r.Customer ?? "-", Dt(r.ReturnDate))));
        await HoldsBlock(false, true, false);
        await ExpiringBatches(false);
        var names = await ProductNames(insp.Select(i => i.ProductId));
        AddList("Recent inspections", "/Warehouses/ReturnsInspection", "No inspections in the last 30 days.", new[] { "Product", "Returned", "Damaged", "Reason" },
            insp.OrderByDescending(i => i.CreatedAtUtc).Take(8).Select(i => Row("/Warehouses/ReturnsInspection", i.Damaged > 0 ? "warn" : null, i.ProductId != null && names.ContainsKey(i.ProductId) ? names[i.ProductId] : "-", Num(i.Returned), Num(i.Damaged), i.ReasonCode ?? "-")));

        var reasons = insp.GroupBy(i => string.IsNullOrEmpty(i.ReasonCode) ? "Not stated" : i.ReasonCode!).Select(g => new { R = g.Key, Q = g.Sum(x => x.Returned) }).OrderByDescending(x => x.Q).Take(6).ToList();
        Chart("Why customers return goods", "hbar", reasons.Select(x => x.R).ToList(), "", "/Warehouses/ReturnsInspection", ("Units", reasons.Select(x => Math.Round(x.Q, 2)).ToList()));
        var days = Range(14);
        Chart("Inspected units per day, 14 days", "bar", days.Select(d => d.ToString("dd MMM")).ToList(), "", "/Warehouses/ReturnsInspection",
            ("Good", days.Select(d => Math.Round(insp.Where(i => i.CreatedAtUtc != null && i.CreatedAtUtc.Value.Date == d).Sum(i => i.Good), 2)).ToList()),
            ("Damaged", days.Select(d => Math.Round(insp.Where(i => i.CreatedAtUtc != null && i.CreatedAtUtc.Value.Date == d).Sum(i => i.Damaged), 2)).ToList()));
        await HoldsBlock(false, false, true);
    }

    private async Task CustomerService()
    {
        var todayOrders = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.OrderDate >= _today && s.OrderDate < _today.AddDays(1), _ct);
        var since7 = _today.AddDays(-6);
        var shippedWeek = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.Status == "Shipped" && s.ShippedAtUtc >= since7, _ct);
        var openReturns = await _context.SalesReturn.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(r => r.Status == SalesReturnStatus.Draft, _ct);
        Kpi("Orders today", todayOrders, "", null, null, "/SalesOrders/SalesOrderList");
        await SalesWaitingDelivery();
        await LateDeliveries(true);
        Kpi("Shipped in the last 7 days", shippedWeek, "", "Shipments handed to the carrier", null, "/Warehouses/Packing");
        Kpi("Returns not yet confirmed", openReturns, "", openReturns == 0 ? "No open returns" : "Waiting for a decision", CountTone(openReturns), "/SalesReturns/SalesReturnList");
        await OrderCycleKpi();

        var ship = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.Status == "Shipped").OrderByDescending(s => s.ShippedAtUtc).Take(8)
            .Select(s => new { s.Number, s.Carrier, s.TrackingNumber, s.ShippedAtUtc }).ToListAsync(_ct);
        AddList("Recently shipped", "/Warehouses/Packing", "Nothing has shipped yet.", new[] { "Shipment", "Carrier", "Tracking", "Shipped" },
            ship.Select(s => Row("/Warehouses/Packing", null, s.Number ?? "-", s.Carrier ?? "-", s.TrackingNumber ?? "-", Dt(s.ShippedAtUtc))));
        var rets = await _context.SalesReturn.AsNoTracking().ApplyIsDeletedFilter(false).Where(r => r.Status == SalesReturnStatus.Draft).OrderBy(r => r.ReturnDate).Take(8)
            .Select(r => new { r.Number, r.ReturnDate, Customer = r.DeliveryOrder!.SalesOrder!.Customer!.Name }).ToListAsync(_ct);
        AddList("Open sales returns", "/SalesReturns/SalesReturnList", "No open returns.", new[] { "Return", "Customer", "Date" },
            rets.Select(r => Row("/SalesReturns/SalesReturnList", "warn", r.Number ?? "-", r.Customer ?? "-", Dt(r.ReturnDate))));

        var days = Range(30);
        var orders = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.OrderDate >= days[0]).Select(s => s.OrderDate).ToListAsync(_ct);
        var returnsPerDay = await _context.SalesReturn.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.ReturnDate >= days[0]).Select(s => s.ReturnDate).ToListAsync(_ct);
        Chart("Orders and returns per day", "line", days.Select(d => d.ToString("dd MMM")).ToList(), "", "/SalesOrders/SalesOrderList",
            ("Orders", days.Select(d => (double)orders.Count(o => o != null && o.Value.Date == d)).ToList()),
            ("Returns", days.Select(d => (double)returnsPerDay.Count(o => o != null && o.Value.Date == d)).ToList()));
        var doStatus = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false).GroupBy(d => d.Status).Select(g => new { S = g.Key, C = g.Count() }).ToListAsync(_ct);
        Chart("Deliveries by status", "donut", doStatus.Select(x => x.S.ToString() ?? "-").ToList(), "", "/DeliveryOrders/DeliveryOrderList", ("Deliveries", doStatus.Select(x => (double)x.C).ToList()));
        await OrdersByStatus(true);
    }

    private async Task Auditor()
    {
        var days14 = Range(14);
        var since14 = days14[0];
        var since30 = _today.AddDays(-29);
        var mv = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.MovementDate >= since30 && (x.ModuleName == "PositiveAdjustment" || x.ModuleName == "NegativeAdjustment" || x.ModuleName == "Scrapping"))
            .Select(x => new { x.MovementDate, x.ModuleName, x.ModuleNumber, x.Movement, x.ProductId, Product = x.Product!.Name }).ToListAsync(_ct);
        double Units(string m) => Math.Round(mv.Where(x => x.ModuleName == m).Sum(x => Math.Abs(x.Movement ?? 0)), 2);
        var audit = await _context.AuditLog.AsNoTracking().ApplyIsDeletedFilter(false).Where(a => a.CreatedAtUtc >= since14).Select(a => new { a.CreatedAtUtc, a.Action, a.EntityName, a.EntityLabel }).ToListAsync(_ct);
        var deleted = audit.Count(a => a.Action == "Deleted");

        Kpi("Units added by adjustment", Units("PositiveAdjustment"), "", "Positive adjustments, 30 days", null, "/PositiveAdjustments/PositiveAdjustmentList");
        Kpi("Units removed by adjustment", Units("NegativeAdjustment"), "", "Negative adjustments, 30 days", Units("NegativeAdjustment") > 0 ? "warn" : "good", "/NegativeAdjustments/NegativeAdjustmentList");
        Kpi("Units scrapped", Units("Scrapping"), "", "Written off as scrap, 30 days", Units("Scrapping") > 0 ? "warn" : "good", "/Scrappings/ScrappingList");
        await CountsKpis();
        Kpi("Records deleted, 14 days", deleted, "", deleted == 0 ? "Nothing was deleted" : "Review each one", CountTone(deleted), null);
        await StockKpis(true, false);
        await ReturnRateKpi();

        AddList("Largest adjustments and write-offs", null, "No adjustments or write-offs in the last 30 days.", new[] { "Date", "Type", "Document", "Product", "Units" },
            mv.OrderByDescending(x => Math.Abs(x.Movement ?? 0)).Take(8).Select(x => Row(null, x.ModuleName == "PositiveAdjustment" ? null : "warn", Dt(x.MovementDate), x.ModuleName ?? "-", x.ModuleNumber ?? "-", x.Product ?? "-", Num(x.Movement ?? 0))));
        var diffs = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.MovementDate >= since30 && x.ModuleName == "StockCount" && x.QtySCDelta != null && x.QtySCDelta != 0)
            .Select(x => new { x.ModuleNumber, Product = x.Product!.Name, x.QtySCSys, x.QtySCCount, x.QtySCDelta }).ToListAsync(_ct);
        AddList("Biggest count differences", "/StockCounts/StockCountList", "No count differences in the last 30 days.", new[] { "Count", "Product", "System", "Counted", "Difference" },
            diffs.OrderByDescending(x => Math.Abs(x.QtySCDelta ?? 0)).Take(8).Select(x => Row("/StockCounts/StockCountList", "warn", x.ModuleNumber ?? "-", x.Product ?? "-", Num(x.QtySCSys ?? 0), Num(x.QtySCCount ?? 0), Num(x.QtySCDelta ?? 0))));
        var del = await _context.AuditLog.AsNoTracking().ApplyIsDeletedFilter(false).Where(a => a.Action == "Deleted").OrderByDescending(a => a.CreatedAtUtc).Take(8)
            .Select(a => new { a.CreatedAtUtc, a.EntityName, a.EntityLabel }).ToListAsync(_ct);
        AddList("Recent deletions", "/Users/AuditLog", "Nothing has been deleted.", new[] { "When", "Record", "Label" },
            del.Select(a => Row("/Users/AuditLog", "bad", a.CreatedAtUtc == null ? "-" : a.CreatedAtUtc.Value.ToString("dd MMM HH:mm"), a.EntityName ?? "-", a.EntityLabel ?? "-")));

        Chart("Adjustments and write-offs per day, 14 days", "bar", days14.Select(d => d.ToString("dd MMM")).ToList(), "", "/Users/AuditLog",
            ("Added", days14.Select(d => Math.Round(mv.Where(x => x.ModuleName == "PositiveAdjustment" && x.MovementDate != null && x.MovementDate.Value.Date == d).Sum(x => Math.Abs(x.Movement ?? 0)), 2)).ToList()),
            ("Removed", days14.Select(d => Math.Round(mv.Where(x => x.ModuleName == "NegativeAdjustment" && x.MovementDate != null && x.MovementDate.Value.Date == d).Sum(x => Math.Abs(x.Movement ?? 0)), 2)).ToList()),
            ("Scrapped", days14.Select(d => Math.Round(mv.Where(x => x.ModuleName == "Scrapping" && x.MovementDate != null && x.MovementDate.Value.Date == d).Sum(x => Math.Abs(x.Movement ?? 0)), 2)).ToList()));
        var byAction = audit.GroupBy(a => a.Action ?? "-").Select(g => new { A = g.Key, C = g.Count() }).ToList();
        Chart("Changes by type, 14 days", "donut", byAction.Select(x => x.A).ToList(), "", "/Users/AuditLog", ("Changes", byAction.Select(x => (double)x.C).ToList()));
        var byEntity = audit.GroupBy(a => a.EntityName ?? "-").Select(g => new { N = g.Key, C = g.Count() }).OrderByDescending(x => x.C).Take(8).ToList();
        Chart("Most changed records, 14 days", "hbar", byEntity.Select(x => x.N).ToList(), "", "/Users/AuditLog", ("Changes", byEntity.Select(x => (double)x.C).ToList()));
        await StockByWarehouse();
    }

    public async Task<GetRoleDashboardResult> Handle(GetRoleDashboardRequest r, CancellationToken ct)
    {
        _ct = ct;
        _now = DateTime.UtcNow;
        _today = _now.Date;
        _uid = r.UserId;
        var persona = PersonaOf(r.Roles ?? new List<string>());

        switch (persona)
        {
            case "admin":
                await AdminBlock();
                break;

            case "manager":
                await SalesBlock(true, false, true);
                await PurchaseBlock(true, false, false);
                await StockKpis(true, true);
                await DockToday(true);
                await RevenueVsPurchases();
                await FlowChart();
                await StockByWarehouse();
                await DeliveriesList("Deliveries to process");
                await PurchaseOrdersAwaiting();
                await LowStockList(false);
                await ExpiringBatches(false);
                break;

            case "finance":
                await SalesBlock(true, false, true);
                await PurchaseBlock(true, false, true);
                await StockKpis(true, false);
                await RevenueVsPurchases();
                await OrdersByStatus(true);
                await StockByWarehouse();
                await LargestOrders(true);
                await LargestOrders(false);
                await PurchaseOrdersAwaiting();
                break;

            case "operator":
                await FloorKpis();
                await DockToday(true);
                await FlowChart();
                await ShipmentsChart();
                await DeliveriesList("Deliveries to pick or ship");
                break;

            case "controller":
                await StockKpis(true, true);
                await ExpiringBatches(true);
                await HoldsBlock(true, true, true);
                await CountsKpis();
                await FlowChart();
                await StockByWarehouse();
                await LowStockList(false);
                break;

            case "buyer":
                await StockKpis(false, true);
                await DraftCounts(false, "Draft purchase orders");
                await PurchaseBlock(true, true, true);
                await PurchaseOrdersAwaiting();
                await OrdersByStatus(false);
                await LowStockList(true);
                await DraftPurchaseList();
                break;

            case "supervisor":
                await Supervisor();
                break;

            case "quality":
                await Quality();
                break;

            case "service":
                await CustomerService();
                break;

            case "auditor":
                await Auditor();
                break;

            case "sales":
                await SalesBlock(true, true, true);
                await DraftCounts(true, "Draft orders to confirm");
                await SalesWaitingDelivery();
                await OrdersByStatus(true);
                await DraftSalesList();
                await RecentSales();
                break;
        }

        return new GetRoleDashboardResult { Persona = persona, Kpis = _kpis, Charts = _charts, Lists = _lists };
    }
}
