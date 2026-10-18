using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ProductManager.Queries;

public class DemandForecastItem
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Stock { get; init; }
    public double TotalSold { get; init; }
    public double AvgDaily { get; init; }
    /// <summary>Change in sales rate, second half of the period vs first half, in percent. Null if there was no early demand.</summary>
    public double? TrendPercent { get; init; }
    public double ForecastNext30Days { get; init; }
    /// <summary>Days until stock runs out at the forecast rate. Null if no demand.</summary>
    public double? DaysOfCover { get; init; }
    public double? CurrentReorderPoint { get; init; }
    public double? CurrentReorderQuantity { get; init; }
    public double RecommendedReorderPoint { get; init; }
    public double RecommendedReorderQuantity { get; init; }
}

public class GetDemandForecastResult
{
    public List<DemandForecastItem>? Data { get; init; }
}

public class GetDemandForecastRequest : IRequest<GetDemandForecastResult>
{
    public int LookbackDays { get; init; } = 90;
    public int LeadTimeDays { get; init; } = 7;
    /// <summary>Target service level in percent (90, 95, 98 or 99).</summary>
    public int ServiceLevel { get; init; } = 95;
}

/// <summary>
/// Statistical demand forecast from delivery history. No external service is used.
/// forecast rate = average of the overall daily rate and the recent-half daily rate (so rising or falling demand counts);
/// safety stock = z * daily standard deviation * sqrt(lead time);
/// reorder point = forecast rate * lead time + safety stock;
/// reorder quantity = 30 days of forecast demand.
/// </summary>
public class GetDemandForecastHandler : IRequestHandler<GetDemandForecastRequest, GetDemandForecastResult>
{
    private readonly IQueryContext _context;

    public GetDemandForecastHandler(IQueryContext context)
    {
        _context = context;
    }

    private static double ZFor(int serviceLevel) => serviceLevel switch
    {
        <= 90 => 1.28,
        <= 95 => 1.65,
        <= 98 => 2.05,
        _ => 2.33
    };

    public async Task<GetDemandForecastResult> Handle(GetDemandForecastRequest request, CancellationToken cancellationToken)
    {
        var days = Math.Clamp(request.LookbackDays, 14, 365);
        var lead = Math.Clamp(request.LeadTimeDays, 1, 120);
        var z = ZFor(request.ServiceLevel);
        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var sales = await _context.InventoryTransaction
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Where(x =>
                x.Status == InventoryTransactionStatus.Confirmed &&
                x.ModuleName == "DeliveryOrder" &&
                x.MovementDate >= since &&
                x.Product!.Physical == true)
            .Select(x => new { x.ProductId, x.MovementDate, x.Stock })
            .ToListAsync(cancellationToken);

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
            .Select(x => new { x.Id, x.Number, x.Name, x.ReorderPoint, x.ReorderQuantity })
            .ToListAsync(cancellationToken);
        var productById = products.ToDictionary(p => p.Id);

        var half = days / 2;
        var result = new List<DemandForecastItem>();

        foreach (var group in sales.Where(s => s.ProductId != null && s.MovementDate != null).GroupBy(s => s.ProductId!))
        {
            if (!productById.TryGetValue(group.Key, out var p)) continue;

            var daily = new double[days];
            foreach (var s in group)
            {
                var idx = (int)(s.MovementDate!.Value.Date - since).TotalDays;
                if (idx >= 0 && idx < days) daily[idx] += Math.Abs(s.Stock ?? 0);
            }

            var total = daily.Sum();
            if (total <= 0) continue;

            var avg = total / days;
            var firstHalf = daily.Take(half).Sum() / half;
            var secondHalf = daily.Skip(days - half).Sum() / half;
            double? trend = firstHalf > 0 ? (secondHalf - firstHalf) / firstHalf * 100 : null;

            var rate = (avg + secondHalf) / 2;
            var variance = daily.Sum(d => (d - avg) * (d - avg)) / days;
            var sigma = Math.Sqrt(variance);

            var reorderPoint = Math.Ceiling(rate * lead + z * sigma * Math.Sqrt(lead));
            var reorderQty = Math.Max(Math.Ceiling(rate * 30), 1);
            var stock = stockByProduct.TryGetValue(p.Id, out var st) ? st : 0.0;

            result.Add(new DemandForecastItem
            {
                ProductId = p.Id,
                ProductNumber = p.Number,
                ProductName = p.Name,
                Stock = stock,
                TotalSold = total,
                AvgDaily = Math.Round(avg, 2),
                TrendPercent = trend.HasValue ? Math.Round(trend.Value, 0) : null,
                ForecastNext30Days = Math.Round(rate * 30, 0),
                DaysOfCover = rate > 0 ? Math.Round(Math.Max(stock, 0) / rate, 0) : null,
                CurrentReorderPoint = p.ReorderPoint,
                CurrentReorderQuantity = p.ReorderQuantity,
                RecommendedReorderPoint = reorderPoint,
                RecommendedReorderQuantity = reorderQty
            });
        }

        return new GetDemandForecastResult
        {
            Data = result.OrderBy(x => x.DaysOfCover ?? double.MaxValue).ToList()
        };
    }
}
