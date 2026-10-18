using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ProductBatchManager.Queries;

public class ProductBatchDto
{
    public string? Id { get; init; }
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ManufactureDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public double Quantity { get; init; }
    /// <summary>Estimated units still in this batch, assuming first-expired-first-out.</summary>
    public double EstimatedRemaining { get; init; }
    public int? DaysToExpiry { get; init; }
    /// <summary>Expired, ExpiringSoon, Ok or NoExpiry.</summary>
    public string Status { get; init; } = "Ok";
    public string? Notes { get; init; }
}

public class ProductBatchSummary
{
    public int BatchCount { get; init; }
    public int ExpiredBatches { get; init; }
    public int ExpiringBatches { get; init; }
    public double ExpiredUnits { get; init; }
    public double ExpiringUnits { get; init; }
}

public class GetProductBatchListResult
{
    public List<ProductBatchDto>? Data { get; init; }
    public ProductBatchSummary? Summary { get; init; }
}

public class GetProductBatchListRequest : IRequest<GetProductBatchListResult>
{
    /// <summary>A batch counts as "expiring soon" when it expires within this many days.</summary>
    public int ExpiringDays { get; init; } = 30;
}

public class GetProductBatchListHandler : IRequestHandler<GetProductBatchListRequest, GetProductBatchListResult>
{
    private readonly IQueryContext _context;

    public GetProductBatchListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetProductBatchListResult> Handle(GetProductBatchListRequest request, CancellationToken cancellationToken)
    {
        var expiringDays = Math.Clamp(request.ExpiringDays, 1, 365);
        var today = DateTime.UtcNow.Date;

        var rows = await _context.ProductBatch
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Select(x => new
            {
                x.Id,
                x.ProductId,
                ProductNumber = x.Product != null ? x.Product.Number : null,
                ProductName = x.Product != null ? x.Product.Name : null,
                x.WarehouseId,
                WarehouseName = x.Warehouse != null ? x.Warehouse.Name : null,
                x.BatchNumber,
                x.ManufactureDate,
                x.ExpiryDate,
                x.Quantity,
                x.Notes
            })
            .ToListAsync(cancellationToken);

        var stockRows = await _context.InventoryTransaction
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed
                        && x.Warehouse!.SystemWarehouse == false
                        && x.Product!.Physical == true)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(cancellationToken);
        var stockByProduct = stockRows
            .Where(x => x.ProductId != null)
            .ToDictionary(x => x.ProductId!, x => x.Stock ?? 0.0);

        // First-expired-first-out: current stock is assumed to sit in the batches that expire last.
        var estimated = new Dictionary<string, double>();
        foreach (var group in rows.Where(r => r.ProductId != null).GroupBy(r => r.ProductId!))
        {
            var remainingStock = Math.Max(stockByProduct.TryGetValue(group.Key, out var s) ? s : 0.0, 0.0);
            foreach (var batch in group.OrderByDescending(b => b.ExpiryDate ?? DateTime.MaxValue).ThenByDescending(b => b.ManufactureDate))
            {
                var take = Math.Min(batch.Quantity ?? 0.0, remainingStock);
                estimated[batch.Id] = take;
                remainingStock -= take;
            }
        }

        var list = rows.Select(r =>
        {
            int? days = r.ExpiryDate.HasValue ? (int)Math.Floor((r.ExpiryDate.Value.Date - today).TotalDays) : null;
            var status = days == null ? "NoExpiry" : days < 0 ? "Expired" : days <= expiringDays ? "ExpiringSoon" : "Ok";
            return new ProductBatchDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductNumber = r.ProductNumber,
                ProductName = r.ProductName,
                WarehouseId = r.WarehouseId,
                WarehouseName = r.WarehouseName,
                BatchNumber = r.BatchNumber,
                ManufactureDate = r.ManufactureDate,
                ExpiryDate = r.ExpiryDate,
                Quantity = r.Quantity ?? 0.0,
                EstimatedRemaining = estimated.TryGetValue(r.Id, out var e) ? e : 0.0,
                DaysToExpiry = days,
                Status = status,
                Notes = r.Notes
            };
        })
        .OrderBy(x => x.ExpiryDate ?? DateTime.MaxValue)
        .ToList();

        var summary = new ProductBatchSummary
        {
            BatchCount = list.Count,
            ExpiredBatches = list.Count(x => x.Status == "Expired" && x.EstimatedRemaining > 0),
            ExpiringBatches = list.Count(x => x.Status == "ExpiringSoon" && x.EstimatedRemaining > 0),
            ExpiredUnits = list.Where(x => x.Status == "Expired").Sum(x => x.EstimatedRemaining),
            ExpiringUnits = list.Where(x => x.Status == "ExpiringSoon").Sum(x => x.EstimatedRemaining)
        };

        return new GetProductBatchListResult { Data = list, Summary = summary };
    }
}
