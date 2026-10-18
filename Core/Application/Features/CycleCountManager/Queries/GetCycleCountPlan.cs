using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CycleCountManager.Queries;

public class CycleCountItemDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    /// <summary>A, B or C by stock value.</summary>
    public string? AbcClass { get; init; }
    public double SystemQty { get; init; }
    public double Value { get; init; }
    public DateTime? LastCounted { get; init; }
    public int? DaysSince { get; init; }
    public int IntervalDays { get; init; }
    public bool Due { get; init; }
    public int OverdueDays { get; init; }
}

public class GetCycleCountPlanResult
{
    public List<CycleCountItemDto> Items { get; init; } = new();
    public int DueA { get; init; }
    public int DueB { get; init; }
    public int DueC { get; init; }
    public int NeverCounted { get; init; }
}

public class GetCycleCountPlanRequest : IRequest<GetCycleCountPlanResult>
{
    public string? WarehouseId { get; init; }
    /// <summary>How often class A / B / C items should be counted, in days.</summary>
    public int DaysA { get; init; } = 30;
    public int DaysB { get; init; } = 90;
    public int DaysC { get; init; } = 180;
}

public class GetCycleCountPlanHandler : IRequestHandler<GetCycleCountPlanRequest, GetCycleCountPlanResult>
{
    private readonly IQueryContext _context;

    public GetCycleCountPlanHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetCycleCountPlanResult> Handle(GetCycleCountPlanRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.WarehouseId)) return new GetCycleCountPlanResult();
        var wh = request.WarehouseId;
        int Days(int v, int d) => v <= 0 ? d : Math.Min(v, 3650);

        var stockRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.WarehouseId == wh && x.Status == InventoryTransactionStatus.Confirmed && x.Product!.Physical == true)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(ct);
        var stock = stockRows.Where(r => r.ProductId != null && (r.Stock ?? 0) > 0).ToDictionary(r => r.ProductId!, r => r.Stock ?? 0.0);
        var pids = stock.Keys.ToList();

        var products = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(p => pids.Contains(p.Id)).Select(p => new { p.Id, p.Number, p.Name, Price = p.UnitPrice ?? 0 }).ToListAsync(ct);

        // last confirmed stock count that included the product in this warehouse
        var counts = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(Domain.Entities.StockCount) && x.WarehouseId == wh && x.Status == InventoryTransactionStatus.Confirmed && pids.Contains(x.ProductId))
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Last = g.Max(x => x.MovementDate) })
            .ToListAsync(ct);
        var last = counts.Where(c => c.ProductId != null).ToDictionary(c => c.ProductId!, c => c.Last);

        // ABC by stock value: A is the top 80 percent of value, B the next 15, C the rest
        var valued = products.Select(p => new { p, Value = stock[p.Id] * p.Price }).OrderByDescending(x => x.Value).ToList();
        var total = valued.Sum(x => x.Value);
        var running = 0.0;
        var classes = new Dictionary<string, string>();
        foreach (var v in valued)
        {
            var before = total > 0 ? running / total : 1;
            classes[v.p.Id] = v.Value <= 0 ? "C" : before < 0.8 ? "A" : before < 0.95 ? "B" : "C";
            running += v.Value;
        }

        var interval = new Dictionary<string, int>
        {
            ["A"] = Days(request.DaysA, 30), ["B"] = Days(request.DaysB, 90), ["C"] = Days(request.DaysC, 180)
        };
        var today = DateTime.UtcNow.Date;
        var items = valued.Select(v =>
        {
            var cls = classes[v.p.Id];
            last.TryGetValue(v.p.Id, out var lc);
            int? since = lc.HasValue ? Math.Max((int)(today - lc.Value.Date).TotalDays, 0) : null;
            var due = since == null || since >= interval[cls];
            return new CycleCountItemDto
            {
                ProductId = v.p.Id, ProductNumber = v.p.Number, ProductName = v.p.Name, AbcClass = cls, SystemQty = stock[v.p.Id], Value = v.Value,
                LastCounted = lc, DaysSince = since, IntervalDays = interval[cls], Due = due,
                OverdueDays = since == null ? 99999 : Math.Max(since.Value - interval[cls], 0)
            };
        })
        .OrderByDescending(i => i.Due).ThenByDescending(i => i.OverdueDays).ThenBy(i => i.AbcClass).ThenByDescending(i => i.Value)
        .ToList();

        return new GetCycleCountPlanResult
        {
            Items = items,
            DueA = items.Count(i => i.Due && i.AbcClass == "A"),
            DueB = items.Count(i => i.Due && i.AbcClass == "B"),
            DueC = items.Count(i => i.Due && i.AbcClass == "C"),
            NeverCounted = items.Count(i => i.DaysSince == null)
        };
    }
}
