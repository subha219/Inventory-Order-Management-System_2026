using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AnalyticsManager.Queries;

public class AnalysisRowDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? GroupName { get; init; }
    public double OnHand { get; init; }
    public double UnitPrice { get; init; }
    public double StockValue { get; init; }
    public double UnitsSold { get; init; }
    public double SalesValue { get; init; }
    public double? CoverDays { get; init; }
    public double? Turnover { get; init; }
    public int? DaysSinceLastIn { get; init; }
    public int? DaysSinceLastSale { get; init; }
    /// <summary>A, B, C by share of sales value, or "-" when nothing was sold.</summary>
    public string AbcClass { get; init; } = "-";
    /// <summary>Fast, Normal, Slow, Dead or No stock.</summary>
    public string Status { get; init; } = "Normal";
    public string? Hint { get; init; }
}

public class AnalysisSummaryDto
{
    public int ClassA { get; init; }
    public int ClassB { get; init; }
    public int ClassC { get; init; }
    public double ValueA { get; init; }
    public double ValueB { get; init; }
    public double ValueC { get; init; }
    public int DeadCount { get; init; }
    public double DeadValue { get; init; }
    public int SlowCount { get; init; }
    public double SlowValue { get; init; }
    public double TotalStockValue { get; init; }
}

public class GetInventoryAnalysisResult
{
    public List<AnalysisRowDto> Rows { get; init; } = new();
    public AnalysisSummaryDto Summary { get; init; } = new();
}

public class GetInventoryAnalysisRequest : IRequest<GetInventoryAnalysisResult>
{
    /// <summary>Sales history window in days (30 to 730).</summary>
    public int Days { get; init; } = 365;
    /// <summary>A product with stock and no sale for this many days is dead stock.</summary>
    public int DeadDays { get; init; } = 90;
}

public class GetInventoryAnalysisHandler : IRequestHandler<GetInventoryAnalysisRequest, GetInventoryAnalysisResult>
{
    private readonly IQueryContext _context;

    public GetInventoryAnalysisHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetInventoryAnalysisResult> Handle(GetInventoryAnalysisRequest r, CancellationToken ct)
    {
        var days = Math.Clamp(r.Days, 30, 730);
        var deadDays = Math.Clamp(r.DeadDays, 7, 730);
        var today = DateTime.UtcNow.Date;
        var since = today.AddDays(-days);

        var products = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.Physical == true)
            .Select(p => new { p.Id, p.Number, p.Name, Group = p.ProductGroup!.Name, p.UnitPrice }).ToListAsync(ct);

        var live = _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false);

        var stockRows = await live.GroupBy(x => x.ProductId).Select(g => new { Id = g.Key, Stock = g.Sum(x => x.Stock) }).ToListAsync(ct);
        var onHand = stockRows.Where(x => x.Id != null).ToDictionary(x => x.Id!, x => x.Stock ?? 0.0);

        var noTransfers = live.Where(x => x.ModuleName != nameof(TransferIn) && x.ModuleName != nameof(TransferOut));
        var sales = noTransfers.Where(x => x.ModuleName == nameof(DeliveryOrder) && x.TransType == InventoryTransType.Out);
        var soldRows = await sales.Where(x => x.MovementDate >= since).GroupBy(x => x.ProductId).Select(g => new { Id = g.Key, Qty = g.Sum(x => x.Movement) }).ToListAsync(ct);
        var sold = soldRows.Where(x => x.Id != null).ToDictionary(x => x.Id!, x => Math.Abs(x.Qty ?? 0));
        var lastSaleRows = await sales.GroupBy(x => x.ProductId).Select(g => new { Id = g.Key, At = g.Max(x => x.MovementDate) }).ToListAsync(ct);
        var lastSale = lastSaleRows.Where(x => x.Id != null && x.At != null).ToDictionary(x => x.Id!, x => x.At!.Value);
        var lastInRows = await noTransfers.Where(x => x.TransType == InventoryTransType.In).GroupBy(x => x.ProductId).Select(g => new { Id = g.Key, At = g.Max(x => x.MovementDate) }).ToListAsync(ct);
        var lastIn = lastInRows.Where(x => x.Id != null && x.At != null).ToDictionary(x => x.Id!, x => x.At!.Value);

        // ABC on sales value
        var valued = products.Select(p => new { p.Id, Value = (sold.TryGetValue(p.Id, out var q) ? q : 0) * (p.UnitPrice ?? 0), Units = sold.TryGetValue(p.Id, out var q2) ? q2 : 0 })
            .Where(x => x.Units > 0).OrderByDescending(x => x.Value).ThenByDescending(x => x.Units).ToList();
        var total = valued.Sum(x => x.Value);
        var abc = new Dictionary<string, string>();
        double cum = 0;
        foreach (var v in valued)
        {
            var share = total > 0 ? cum / total : 0;
            abc[v.Id] = share < 0.80 ? "A" : share < 0.95 ? "B" : "C";
            cum += v.Value;
        }

        var rows = new List<AnalysisRowDto>();
        foreach (var p in products)
        {
            var stock = onHand.TryGetValue(p.Id, out var s) ? s : 0;
            var units = sold.TryGetValue(p.Id, out var u) ? u : 0;
            var price = p.UnitPrice ?? 0;
            double? cover = units > 0 ? Math.Round(Math.Max(stock, 0) / (units / days), 1) : null;
            double? turn = stock > 0 && units > 0 ? Math.Round(units * (365.0 / days) / stock, 2) : null;
            int? sinceSale = lastSale.TryGetValue(p.Id, out var ls) ? (int)(today - ls.Date).TotalDays : null;
            int? sinceIn = lastIn.TryGetValue(p.Id, out var li) ? (int)(today - li.Date).TotalDays : null;
            var cls = abc.TryGetValue(p.Id, out var c) ? c : "-";

            string status; string? hint;
            if (stock <= 0) { status = "No stock"; hint = cls == "A" ? "Fast mover with no stock: reorder now" : null; }
            else if (units == 0 || (sinceSale ?? int.MaxValue) > deadDays) { status = "Dead"; hint = "No recent sales: discount, return to vendor or scrap, and free the bin"; }
            else if (cover > 180) { status = "Slow"; hint = "More than six months of cover: stop reordering, keep in reserve storage"; }
            else if (cover <= 30) { status = "Fast"; hint = cls == "A" ? "Keep in a pick face close to dispatch and replenish often" : "Keep stock near the pick area"; }
            else { status = "Normal"; hint = cls == "C" ? "Low value item: store in reserve or upper shelves" : null; }

            rows.Add(new AnalysisRowDto
            {
                ProductId = p.Id, ProductNumber = p.Number, ProductName = p.Name, GroupName = p.Group, OnHand = stock, UnitPrice = price, StockValue = Math.Max(stock, 0) * price,
                UnitsSold = units, SalesValue = units * price, CoverDays = cover, Turnover = turn, DaysSinceLastIn = sinceIn, DaysSinceLastSale = sinceSale, AbcClass = cls, Status = status, Hint = hint
            });
        }

        var summary = new AnalysisSummaryDto
        {
            ClassA = rows.Count(x => x.AbcClass == "A"), ClassB = rows.Count(x => x.AbcClass == "B"), ClassC = rows.Count(x => x.AbcClass == "C"),
            ValueA = rows.Where(x => x.AbcClass == "A").Sum(x => x.SalesValue), ValueB = rows.Where(x => x.AbcClass == "B").Sum(x => x.SalesValue), ValueC = rows.Where(x => x.AbcClass == "C").Sum(x => x.SalesValue),
            DeadCount = rows.Count(x => x.Status == "Dead"), DeadValue = rows.Where(x => x.Status == "Dead").Sum(x => x.StockValue),
            SlowCount = rows.Count(x => x.Status == "Slow"), SlowValue = rows.Where(x => x.Status == "Slow").Sum(x => x.StockValue),
            TotalStockValue = rows.Sum(x => x.StockValue)
        };
        return new GetInventoryAnalysisResult { Rows = rows.OrderByDescending(x => x.SalesValue).ThenByDescending(x => x.StockValue).ToList(), Summary = summary };
    }
}
