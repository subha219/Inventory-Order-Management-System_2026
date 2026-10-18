using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DashboardManager.Queries;

public class StockAlertItem
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Stock { get; init; }
    public double ReorderPoint { get; init; }
    public double? ReorderQuantity { get; init; }
    /// <summary>How many units below the reorder point (0 when exactly at it).</summary>
    public double Shortfall { get; init; }
}

public class ValueItem
{
    public string? ProductName { get; init; }
    public double Stock { get; init; }
    public double Value { get; init; }
}

public class DailyMovementItem
{
    public string Date { get; init; } = string.Empty;
    public double UnitsIn { get; init; }
    public double UnitsOut { get; init; }
}

public class GetInsightsDashboardDto
{
    public int TotalSkus { get; init; }
    public double InventoryValue { get; init; }
    public int LowStockCount { get; init; }
    public int OutOfStockCount { get; init; }
    /// <summary>Units received over the last 14 days.</summary>
    public double UnitsInPeriod { get; init; }
    public double UnitsOutPeriod { get; init; }
    public List<StockAlertItem> LowStockItems { get; init; } = new();
    public List<ValueItem> TopValueItems { get; init; } = new();
    public List<DailyMovementItem> DailyMovement { get; init; } = new();
}

public class GetInsightsDashboardResult
{
    public GetInsightsDashboardDto? Data { get; init; }
}

public class GetInsightsDashboardRequest : IRequest<GetInsightsDashboardResult>
{
}

/// <summary>
/// Aggregates the numbers behind the new dashboard: stock value, low/out-of-stock alerts based on each
/// product's reorder point, top items by value and 14-day in/out movement.
/// Inventory value uses the product's unit price (the system has no separate cost field yet).
/// </summary>
public class GetInsightsDashboardHandler : IRequestHandler<GetInsightsDashboardRequest, GetInsightsDashboardResult>
{
    private const int MovementDays = 14;
    private readonly IQueryContext _context;

    public GetInsightsDashboardHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetInsightsDashboardResult> Handle(GetInsightsDashboardRequest request, CancellationToken cancellationToken)
    {
        // Confirmed stock per product across all real (non-system) warehouses.
        var stockRows = await _context.InventoryTransaction
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Where(x =>
                x.Status == InventoryTransactionStatus.Confirmed &&
                x.Warehouse!.SystemWarehouse == false &&
                x.Product!.Physical == true)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(cancellationToken);

        var stockByProduct = stockRows
            .Where(x => x.ProductId != null)
            .ToDictionary(x => x.ProductId!, x => x.Stock ?? 0.0);

        var products = await _context.Product
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Where(x => x.Physical == true)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Name,
                x.UnitPrice,
                x.ReorderPoint,
                x.ReorderQuantity
            })
            .ToListAsync(cancellationToken);

        var rows = products
            .Select(p => new
            {
                Product = p,
                Stock = stockByProduct.TryGetValue(p.Id, out var s) ? s : 0.0
            })
            .ToList();

        var lowStock = rows
            .Where(r => r.Product.ReorderPoint.HasValue && r.Stock <= r.Product.ReorderPoint.Value)
            .Select(r => new StockAlertItem
            {
                ProductId = r.Product.Id,
                ProductNumber = r.Product.Number,
                ProductName = r.Product.Name,
                Stock = r.Stock,
                ReorderPoint = r.Product.ReorderPoint!.Value,
                ReorderQuantity = r.Product.ReorderQuantity,
                Shortfall = r.Product.ReorderPoint!.Value - r.Stock
            })
            .OrderByDescending(x => x.Shortfall)
            .ToList();

        var topValue = rows
            .Where(r => r.Stock > 0 && (r.Product.UnitPrice ?? 0) > 0)
            .Select(r => new ValueItem
            {
                ProductName = r.Product.Name,
                Stock = r.Stock,
                Value = r.Stock * (r.Product.UnitPrice ?? 0)
            })
            .OrderByDescending(x => x.Value)
            .Take(5)
            .ToList();

        // Movement for the last N days (confirmed, real warehouses). Grouped in memory to stay provider-agnostic.
        var since = DateTime.UtcNow.Date.AddDays(-(MovementDays - 1));
        var movementRows = await _context.InventoryTransaction
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Where(x =>
                x.Status == InventoryTransactionStatus.Confirmed &&
                x.MovementDate >= since &&
                x.Warehouse!.SystemWarehouse == false &&
                x.Product!.Physical == true)
            .Select(x => new { x.MovementDate, x.Stock })
            .ToListAsync(cancellationToken);

        var daily = Enumerable.Range(0, MovementDays)
            .Select(i => since.AddDays(i))
            .Select(day =>
            {
                var dayRows = movementRows.Where(m => m.MovementDate!.Value.Date == day).ToList();
                return new DailyMovementItem
                {
                    Date = day.ToString("dd MMM"),
                    UnitsIn = dayRows.Where(m => (m.Stock ?? 0) > 0).Sum(m => m.Stock ?? 0),
                    UnitsOut = dayRows.Where(m => (m.Stock ?? 0) < 0).Sum(m => -(m.Stock ?? 0))
                };
            })
            .ToList();

        var dto = new GetInsightsDashboardDto
        {
            TotalSkus = rows.Count,
            InventoryValue = rows.Sum(r => Math.Max(r.Stock, 0) * (r.Product.UnitPrice ?? 0)),
            LowStockCount = lowStock.Count(x => x.Stock > 0),
            OutOfStockCount = rows.Count(r => r.Stock <= 0 && r.Product.ReorderPoint.HasValue),
            UnitsInPeriod = daily.Sum(d => d.UnitsIn),
            UnitsOutPeriod = daily.Sum(d => d.UnitsOut),
            LowStockItems = lowStock.Take(20).ToList(),
            TopValueItems = topValue,
            DailyMovement = daily
        };

        return new GetInsightsDashboardResult { Data = dto };
    }
}
