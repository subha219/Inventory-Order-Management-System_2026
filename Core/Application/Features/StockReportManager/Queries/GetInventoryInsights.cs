using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StockReportManager.Queries;

public class InsightRowDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? GroupName { get; init; }
    public double OnHand { get; init; }
    public double UnitPrice { get; init; }
    public double Value { get; init; }
    public DateTime? LastSold { get; init; }
    public int? DaysSinceSold { get; init; }
    /// <summary>Active, Slow (no sale for the chosen number of days) or Dead (never sold).</summary>
    public string? Movement { get; init; }
}

public class InsightGroupDto
{
    public string? GroupName { get; init; }
    public double Value { get; init; }
    public double Units { get; init; }
    public int Products { get; init; }
}

public class GetInventoryInsightsResult
{
    public List<InsightRowDto> Rows { get; init; } = new();
    public List<InsightGroupDto> Groups { get; init; } = new();
    public double TotalValue { get; init; }
    public double TotalUnits { get; init; }
    public double SlowValue { get; init; }
    public double DeadValue { get; init; }
    public int SlowCount { get; init; }
    public int DeadCount { get; init; }
}

public class GetInventoryInsightsRequest : IRequest<GetInventoryInsightsResult>
{
    public string? WarehouseId { get; init; }
    public int SlowDays { get; init; } = 90;
}

public class GetInventoryInsightsHandler : IRequestHandler<GetInventoryInsightsRequest, GetInventoryInsightsResult>
{
    private readonly IQueryContext _context;

    public GetInventoryInsightsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetInventoryInsightsResult> Handle(GetInventoryInsightsRequest request, CancellationToken ct)
    {
        var slowDays = Math.Clamp(request.SlowDays <= 0 ? 90 : request.SlowDays, 1, 3650);
        var wh = string.IsNullOrWhiteSpace(request.WarehouseId) ? null : request.WarehouseId;

        var stockRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false && x.Product!.Physical == true &&
                        (wh == null || x.WarehouseId == wh))
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(ct);
        var stock = stockRows.Where(r => r.ProductId != null && (r.Stock ?? 0) > 0).ToDictionary(r => r.ProductId!, r => r.Stock ?? 0.0);
        var pids = stock.Keys.ToList();

        var products = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(p => pids.Contains(p.Id))
            .Select(p => new { p.Id, p.Number, p.Name, Price = p.UnitPrice ?? 0, Group = p.ProductGroup!.Name }).ToListAsync(ct);

        var sold = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(Domain.Entities.DeliveryOrder) && x.Status == InventoryTransactionStatus.Confirmed && pids.Contains(x.ProductId))
            .GroupBy(x => x.ProductId).Select(g => new { ProductId = g.Key, Last = g.Max(x => x.MovementDate) }).ToListAsync(ct);
        var lastSold = sold.Where(s => s.ProductId != null).ToDictionary(s => s.ProductId!, s => s.Last);

        var today = DateTime.UtcNow.Date;
        var rows = products.Select(p =>
        {
            var qty = stock[p.Id];
            lastSold.TryGetValue(p.Id, out var ls);
            int? days = ls.HasValue ? Math.Max((int)(today - ls.Value.Date).TotalDays, 0) : null;
            var movement = days == null ? "Dead" : days >= slowDays ? "Slow" : "Active";
            return new InsightRowDto
            {
                ProductId = p.Id, ProductNumber = p.Number, ProductName = p.Name, GroupName = p.Group ?? "No group", OnHand = qty,
                UnitPrice = p.Price, Value = qty * p.Price, LastSold = ls, DaysSinceSold = days, Movement = movement
            };
        }).OrderByDescending(r => r.Value).ToList();

        return new GetInventoryInsightsResult
        {
            Rows = rows,
            Groups = rows.GroupBy(r => r.GroupName).Select(g => new InsightGroupDto { GroupName = g.Key, Value = g.Sum(r => r.Value), Units = g.Sum(r => r.OnHand), Products = g.Count() })
                .OrderByDescending(g => g.Value).ToList(),
            TotalValue = rows.Sum(r => r.Value),
            TotalUnits = rows.Sum(r => r.OnHand),
            SlowValue = rows.Where(r => r.Movement == "Slow").Sum(r => r.Value),
            DeadValue = rows.Where(r => r.Movement == "Dead").Sum(r => r.Value),
            SlowCount = rows.Count(r => r.Movement == "Slow"),
            DeadCount = rows.Count(r => r.Movement == "Dead")
        };
    }
}
