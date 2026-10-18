using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DashboardManager.Queries;

public class AnalyticsKpi
{
    public double Current { get; init; }
    public double Previous { get; init; }
    /// <summary>Percent change versus the previous period; null when there is nothing to compare with.</summary>
    public double? ChangePercent { get; init; }
}

public class AnalyticsDailyPoint
{
    public string Date { get; init; } = string.Empty;
    public double Sales { get; init; }
    public double Purchases { get; init; }
}

public class AnalyticsRankItem
{
    public string? Name { get; init; }
    public string? Sub { get; init; }
    public double Units { get; init; }
    public double Amount { get; init; }
    public double SharePercent { get; init; }
}

public class AnalyticsAbcBucket
{
    public string Class { get; init; } = string.Empty;
    public int Products { get; init; }
    public double Value { get; init; }
    public double SharePercent { get; init; }
}

public class AnalyticsInsight
{
    /// <summary>good, info, warning or danger.</summary>
    public string Level { get; init; } = "info";
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string? Link { get; init; }
    public string? LinkText { get; init; }
}

public class GetAnalyticsDashboardDto
{
    public int Days { get; init; }
    public AnalyticsKpi Revenue { get; init; } = new();
    public AnalyticsKpi Orders { get; init; } = new();
    public AnalyticsKpi AverageOrderValue { get; init; } = new();
    public AnalyticsKpi Spend { get; init; } = new();
    public double ProjectedSalesNext7Days { get; init; }
    public List<AnalyticsDailyPoint> Daily { get; init; } = new();
    public List<AnalyticsRankItem> TopProducts { get; init; } = new();
    public List<AnalyticsRankItem> TopCustomers { get; init; } = new();
    public List<AnalyticsAbcBucket> Abc { get; init; } = new();
    public int DraftSalesOrders { get; init; }
    public double DraftSalesAmount { get; init; }
    public int DraftPurchaseOrders { get; init; }
    public double DraftPurchaseAmount { get; init; }
    public int DeadStockProducts { get; init; }
    public double DeadStockValue { get; init; }
    /// <summary>0-100 score combining stock availability, dead stock and open alerts.</summary>
    public int HealthScore { get; init; }
    public List<AnalyticsInsight> Insights { get; init; } = new();
}

public class GetAnalyticsDashboardResult
{
    public GetAnalyticsDashboardDto? Data { get; init; }
}

public class GetAnalyticsDashboardRequest : IRequest<GetAnalyticsDashboardResult>
{
    public int Days { get; init; } = 30;
}

/// <summary>
/// Sales/purchase KPIs with period-over-period change, a daily trend, top products and customers, ABC stock
/// classification, dead stock and a list of plain-language insights. All numbers are computed from confirmed orders.
/// </summary>
public class GetAnalyticsDashboardHandler : IRequestHandler<GetAnalyticsDashboardRequest, GetAnalyticsDashboardResult>
{
    private readonly IQueryContext _context;

    public GetAnalyticsDashboardHandler(IQueryContext context)
    {
        _context = context;
    }

    private static double? Change(double cur, double prev) => prev > 0 ? Math.Round((cur - prev) / prev * 100, 1) : null;

    private static AnalyticsKpi Kpi(double cur, double prev) => new() { Current = cur, Previous = prev, ChangePercent = Change(cur, prev) };

    private static string Money(double v) => v.ToString("#,##0");

    public async Task<GetAnalyticsDashboardResult> Handle(GetAnalyticsDashboardRequest request, CancellationToken ct)
    {
        var days = request.Days switch { 7 => 7, 90 => 90, _ => 30 };
        var today = DateTime.UtcNow.Date;
        var start = today.AddDays(-(days - 1));
        var prevStart = start.AddDays(-days);
        var deadWindowStart = today.AddDays(-90);
        var fetchFrom = prevStart < deadWindowStart ? prevStart : deadWindowStart;

        // ---- confirmed sales and purchase orders (current + previous period) ----
        var sales = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.OrderStatus == SalesOrderStatus.Confirmed && x.OrderDate >= fetchFrom)
            .Select(x => new { x.Id, x.OrderDate, x.CustomerId, Amount = x.AfterTaxAmount ?? 0 })
            .ToListAsync(ct);

        var purchases = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.OrderStatus == PurchaseOrderStatus.Confirmed && x.OrderDate >= prevStart)
            .Select(x => new { x.OrderDate, Amount = x.AfterTaxAmount ?? 0 })
            .ToListAsync(ct);

        var curSales = sales.Where(s => s.OrderDate!.Value.Date >= start).ToList();
        var prevSales = sales.Where(s => s.OrderDate!.Value.Date >= prevStart && s.OrderDate!.Value.Date < start).ToList();
        var curPurch = purchases.Where(s => s.OrderDate!.Value.Date >= start).ToList();
        var prevPurch = purchases.Where(s => s.OrderDate!.Value.Date < start).ToList();

        var revenue = curSales.Sum(s => s.Amount);
        var prevRevenue = prevSales.Sum(s => s.Amount);
        var aov = curSales.Count > 0 ? revenue / curSales.Count : 0;
        var prevAov = prevSales.Count > 0 ? prevRevenue / prevSales.Count : 0;

        // ---- daily trend (for 90 days the chart is still one point per day) ----
        var daily = Enumerable.Range(0, days).Select(i => start.AddDays(i)).Select(d => new AnalyticsDailyPoint
        {
            Date = d.ToString("dd MMM"),
            Sales = Math.Round(curSales.Where(s => s.OrderDate!.Value.Date == d).Sum(s => s.Amount), 2),
            Purchases = Math.Round(curPurch.Where(s => s.OrderDate!.Value.Date == d).Sum(s => s.Amount), 2)
        }).ToList();

        // Simple projection: average daily sales of the last 14 days (or the whole period when shorter), times 7.
        var projWindow = Math.Min(14, days);
        var recent = daily.TakeLast(projWindow).Sum(d => d.Sales) / projWindow;
        var projected = Math.Round(recent * 7, 0);

        // ---- items for top products / customers / dead stock ----
        var items = await _context.SalesOrderItem.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.SalesOrder!.OrderStatus == SalesOrderStatus.Confirmed && x.SalesOrder.OrderDate >= deadWindowStart)
            .Select(x => new { x.ProductId, x.SalesOrder!.OrderDate, Qty = x.Quantity ?? 0, Total = x.Total ?? 0 })
            .ToListAsync(ct);

        var products = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Physical == true)
            .Select(x => new { x.Id, x.Number, x.Name, x.UnitPrice, x.ReorderPoint })
            .ToListAsync(ct);
        var productById = products.ToDictionary(p => p.Id);

        var curItems = items.Where(i => i.OrderDate!.Value.Date >= start).ToList();
        var itemRevenue = curItems.Sum(i => i.Total);
        var topProducts = curItems.Where(i => i.ProductId != null)
            .GroupBy(i => i.ProductId!)
            .Select(g => new { Id = g.Key, Units = g.Sum(i => i.Qty), Amount = g.Sum(i => i.Total) })
            .OrderByDescending(x => x.Amount).Take(5)
            .Select(x => new AnalyticsRankItem
            {
                Name = productById.TryGetValue(x.Id, out var p) ? p.Name : "(deleted product)",
                Sub = productById.TryGetValue(x.Id, out var p2) ? p2.Number : null,
                Units = x.Units,
                Amount = x.Amount,
                SharePercent = itemRevenue > 0 ? Math.Round(x.Amount / itemRevenue * 100, 1) : 0
            }).ToList();

        var customerNames = await _context.Customer.AsNoTracking().ApplyIsDeletedFilter(false)
            .Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var topCustomers = curSales.Where(s => s.CustomerId != null)
            .GroupBy(s => s.CustomerId!)
            .Select(g => new { Id = g.Key, Orders = g.Count(), Amount = g.Sum(s => s.Amount) })
            .OrderByDescending(x => x.Amount).Take(5)
            .Select(x => new AnalyticsRankItem
            {
                Name = customerNames.TryGetValue(x.Id, out var n) ? n : "(deleted customer)",
                Sub = x.Orders + (x.Orders == 1 ? " order" : " orders"),
                Units = x.Orders,
                Amount = x.Amount,
                SharePercent = revenue > 0 ? Math.Round(x.Amount / revenue * 100, 1) : 0
            }).ToList();

        // ---- current stock + ABC + dead stock ----
        var stockRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed &&
                        x.Warehouse!.SystemWarehouse == false && x.Product!.Physical == true)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(ct);
        var stock = stockRows.Where(r => r.ProductId != null).ToDictionary(r => r.ProductId!, r => r.Stock ?? 0.0);

        var valued = products
            .Select(p => new { P = p, Stock = stock.TryGetValue(p.Id, out var s) ? s : 0.0 })
            .Select(x => new { x.P, x.Stock, Value = Math.Max(x.Stock, 0) * (x.P.UnitPrice ?? 0) })
            .Where(x => x.Value > 0).OrderByDescending(x => x.Value).ToList();
        var totalValue = valued.Sum(x => x.Value);
        var abcCount = new[] { 0, 0, 0 };
        var abcValue = new[] { 0.0, 0.0, 0.0 };
        var running = 0.0;
        foreach (var v in valued)
        {
            var pctBefore = totalValue > 0 ? running / totalValue : 0;
            var idx = pctBefore < 0.80 ? 0 : (pctBefore < 0.95 ? 1 : 2);
            abcCount[idx]++; abcValue[idx] += v.Value; running += v.Value;
        }
        var abc = new[] { "A", "B", "C" }.Select((c, i) => new AnalyticsAbcBucket
        {
            Class = c, Products = abcCount[i], Value = abcValue[i],
            SharePercent = totalValue > 0 ? Math.Round(abcValue[i] / totalValue * 100, 1) : 0
        }).ToList();

        var soldIds = items.Where(i => i.ProductId != null && i.Qty > 0).Select(i => i.ProductId!).ToHashSet();
        var dead = valued.Where(x => !soldIds.Contains(x.P.Id)).ToList();
        var deadValue = dead.Sum(x => x.Value);

        // ---- open drafts ----
        var draftSales = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.OrderStatus == SalesOrderStatus.Draft).Select(x => new { x.OrderDate, Amount = x.AfterTaxAmount ?? 0 }).ToListAsync(ct);
        var draftPurch = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.OrderStatus == PurchaseOrderStatus.Draft).Select(x => new { x.OrderDate, Amount = x.AfterTaxAmount ?? 0 }).ToListAsync(ct);

        // ---- stock-out risk: cover below 14 days using the last 30 days of confirmed sales ----
        var rateSince = today.AddDays(-29);
        var rate = items.Where(i => i.ProductId != null && i.OrderDate!.Value.Date >= rateSince)
            .GroupBy(i => i.ProductId!).ToDictionary(g => g.Key, g => g.Sum(i => i.Qty) / 30.0);
        var atRisk = products
            .Select(p => new { P = p, Stock = stock.TryGetValue(p.Id, out var s) ? s : 0.0, Rate = rate.TryGetValue(p.Id, out var r) ? r : 0.0 })
            .Where(x => x.Rate > 0 && x.Stock >= 0 && x.Stock / x.Rate < 14)
            .OrderBy(x => x.Stock / x.Rate).ToList();
        var lowCount = products.Count(p => p.ReorderPoint.HasValue && (stock.TryGetValue(p.Id, out var s) ? s : 0) <= p.ReorderPoint.Value);

        // ---- expiring batches (table may not exist on an old database) ----
        int expired = 0, expiring = 0;
        try
        {
            var limit = DateTime.UtcNow.Date.AddDays(30);
            var batchDates = await _context.ProductBatch.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(b => b.ExpiryDate != null && b.ExpiryDate <= limit && (b.Quantity ?? 0) > 0)
                .Select(b => b.ExpiryDate!.Value).ToListAsync(ct);
            expired = batchDates.Count(d => d < DateTime.UtcNow.Date);
            expiring = batchDates.Count - expired;
        }
        catch { /* batch tracking not available */ }

        // ---- insights ----
        var insights = new List<AnalyticsInsight>();
        var ch = Change(revenue, prevRevenue);
        if (ch.HasValue)
        {
            insights.Add(ch.Value >= 0
                ? new AnalyticsInsight { Level = "good", Title = $"Sales are up {ch.Value:0.#}%", Detail = $"{Money(revenue)} in the last {days} days versus {Money(prevRevenue)} in the {days} days before." }
                : new AnalyticsInsight { Level = ch.Value <= -20 ? "danger" : "warning", Title = $"Sales are down {Math.Abs(ch.Value):0.#}%", Detail = $"{Money(revenue)} in the last {days} days versus {Money(prevRevenue)} before. Check top customers and stock availability.", Link = "/SalesReports/SalesReportList", LinkText = "Sales report" });
        }
        else if (revenue == 0)
        {
            insights.Add(new AnalyticsInsight { Level = "info", Title = "No confirmed sales in this period", Detail = "Confirm sales orders to see revenue, trends and rankings here.", Link = "/SalesOrders/SalesOrderList", LinkText = "Sales orders" });
        }
        if (atRisk.Count > 0)
        {
            var first = atRisk[0];
            insights.Add(new AnalyticsInsight { Level = "danger", Title = $"{atRisk.Count} product(s) may run out within 14 days", Detail = $"Most urgent: {first.P.Name} (about {Math.Floor(first.Stock / first.Rate)} day(s) left at the current sales rate).", Link = "/PurchaseOrders/ReorderSuggestions", LinkText = "Reorder now" });
        }
        else if (lowCount > 0)
        {
            insights.Add(new AnalyticsInsight { Level = "warning", Title = $"{lowCount} product(s) at or below reorder point", Detail = "Turn them into a draft purchase order in one click.", Link = "/PurchaseOrders/ReorderSuggestions", LinkText = "Reorder suggestions" });
        }
        if (deadValue > 0 && totalValue > 0 && deadValue / totalValue >= 0.1)
        {
            insights.Add(new AnalyticsInsight { Level = "warning", Title = $"{Money(deadValue)} tied up in stock that has not sold in 90 days", Detail = $"{dead.Count} product(s), {Math.Round(deadValue / totalValue * 100)}% of stock value. Consider a promotion, a return to the vendor, or stopping reorders.", Link = "/Products/DemandForecast", LinkText = "Demand forecast" });
        }
        if (topCustomers.Count > 0 && topCustomers[0].SharePercent >= 40 && curSales.Select(s => s.CustomerId).Distinct().Count() > 1)
        {
            insights.Add(new AnalyticsInsight { Level = "warning", Title = $"{topCustomers[0].Name} is {topCustomers[0].SharePercent:0.#}% of your sales", Detail = "Depending on one customer is risky. Consider broadening your customer base." });
        }
        var oldDraftSales = draftSales.Count(d => d.OrderDate.HasValue && d.OrderDate.Value.Date <= today.AddDays(-7));
        if (oldDraftSales > 0)
        {
            insights.Add(new AnalyticsInsight { Level = "info", Title = $"{oldDraftSales} sales order(s) have been in draft for over a week", Detail = "Confirm or cancel them so revenue and stock stay accurate.", Link = "/SalesOrders/SalesOrderList", LinkText = "Sales orders" });
        }
        if (expired + expiring > 0)
        {
            insights.Add(new AnalyticsInsight { Level = expired > 0 ? "danger" : "warning", Title = $"{expired} expired and {expiring} batch(es) expiring within 30 days", Detail = "Sell, discount or remove them before they become waste.", Link = "/Products/ProductBatches", LinkText = "Batches & expiry" });
        }
        if (insights.Count == 0)
        {
            insights.Add(new AnalyticsInsight { Level = "good", Title = "Everything looks healthy", Detail = "No stock risks, expiries or unusual changes detected." });
        }

        // ---- health score ----
        var score = 100;
        score -= Math.Min(30, atRisk.Count * 6);
        score -= Math.Min(15, lowCount * 3);
        score -= totalValue > 0 ? (int)Math.Min(25, deadValue / totalValue * 100) : 0;
        score -= Math.Min(20, expired * 5 + expiring * 2);
        score -= Math.Min(10, oldDraftSales * 2);
        score = Math.Clamp(score, 0, 100);

        var dto = new GetAnalyticsDashboardDto
        {
            Days = days,
            Revenue = Kpi(revenue, prevRevenue),
            Orders = Kpi(curSales.Count, prevSales.Count),
            AverageOrderValue = Kpi(aov, prevAov),
            Spend = Kpi(curPurch.Sum(p => p.Amount), prevPurch.Sum(p => p.Amount)),
            ProjectedSalesNext7Days = projected,
            Daily = daily,
            TopProducts = topProducts,
            TopCustomers = topCustomers,
            Abc = abc,
            DraftSalesOrders = draftSales.Count,
            DraftSalesAmount = draftSales.Sum(d => d.Amount),
            DraftPurchaseOrders = draftPurch.Count,
            DraftPurchaseAmount = draftPurch.Sum(d => d.Amount),
            DeadStockProducts = dead.Count,
            DeadStockValue = deadValue,
            HealthScore = score,
            Insights = insights
        };
        return new GetAnalyticsDashboardResult { Data = dto };
    }
}
