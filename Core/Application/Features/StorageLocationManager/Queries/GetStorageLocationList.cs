using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StorageLocationManager.Queries;

public class LocationItemDto
{
    public string? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? ProductNumber { get; init; }
    public double Quantity { get; init; }
}

public class StorageLocationDto
{
    public string? Id { get; init; }
    public string? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string? Code { get; init; }
    public string? Zone { get; init; }
    public string? Aisle { get; init; }
    public string? Rack { get; init; }
    public string? Shelf { get; init; }
    public string? Bin { get; init; }
    public string? LocationType { get; init; }
    public double? Capacity { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public double Used { get; init; }
    /// <summary>Percent of capacity in use; null when the location has no capacity limit.</summary>
    public double? Utilization { get; init; }
    public List<LocationItemDto> Items { get; init; } = new();
}

public class ZoneSummaryDto
{
    public string? Zone { get; init; }
    public int Locations { get; init; }
    public int Empty { get; init; }
    public double Capacity { get; init; }
    public double Used { get; init; }
    public double Percent { get; init; }
}

public class UnplacedItemDto
{
    public string? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? ProductNumber { get; init; }
    public double OnHand { get; init; }
    public double Placed { get; init; }
    public double Unplaced { get; init; }
}

public class StorageLocationSummaryDto
{
    public int TotalLocations { get; init; }
    public int ActiveLocations { get; init; }
    public int EmptyLocations { get; init; }
    public int FullLocations { get; init; }
    public double TotalCapacity { get; init; }
    public double TotalUsed { get; init; }
    public double UtilizationPercent { get; init; }
    public double UnplacedUnits { get; init; }
}

public class GetStorageLocationListResult
{
    public List<StorageLocationDto>? Data { get; init; }
    public StorageLocationSummaryDto? Summary { get; init; }
    public List<ZoneSummaryDto>? Zones { get; init; }
    public List<UnplacedItemDto>? Unplaced { get; init; }
}

public class GetStorageLocationListRequest : IRequest<GetStorageLocationListResult>
{
    public string? WarehouseId { get; init; }
}

public class GetStorageLocationListHandler : IRequestHandler<GetStorageLocationListRequest, GetStorageLocationListResult>
{
    private readonly IQueryContext _context;

    public GetStorageLocationListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetStorageLocationListResult> Handle(GetStorageLocationListRequest request, CancellationToken ct)
    {
        var warehouseId = string.IsNullOrWhiteSpace(request.WarehouseId) ? null : request.WarehouseId;

        var locQuery = _context.StorageLocation.AsNoTracking().ApplyIsDeletedFilter(false);
        if (warehouseId != null) locQuery = locQuery.Where(x => x.WarehouseId == warehouseId);
        var locs = await locQuery
            .Select(x => new
            {
                x.Id, x.WarehouseId,
                WarehouseName = x.Warehouse != null ? x.Warehouse.Name : null,
                x.Code, x.Zone, x.Aisle, x.Rack, x.Shelf, x.Bin, x.LocationType, x.Capacity, x.IsActive, x.Notes
            })
            .ToListAsync(ct);

        var stockQuery = _context.LocationStock.AsNoTracking().ApplyIsDeletedFilter(false).Where(x => (x.Quantity ?? 0) > 0);
        if (warehouseId != null) stockQuery = stockQuery.Where(x => x.Location!.WarehouseId == warehouseId);
        var stocks = await stockQuery
            .Select(x => new
            {
                x.LocationId, x.ProductId,
                ProductName = x.Product != null ? x.Product.Name : null,
                ProductNumber = x.Product != null ? x.Product.Number : null,
                Qty = x.Quantity ?? 0
            })
            .ToListAsync(ct);
        var byLocation = stocks.Where(s => s.LocationId != null).GroupBy(s => s.LocationId!).ToDictionary(g => g.Key, g => g.ToList());

        var list = locs.Select(l =>
        {
            var items = byLocation.TryGetValue(l.Id, out var s) ? s : null;
            var used = items?.Sum(i => i.Qty) ?? 0;
            return new StorageLocationDto
            {
                Id = l.Id, WarehouseId = l.WarehouseId, WarehouseName = l.WarehouseName, Code = l.Code, Zone = l.Zone,
                Aisle = l.Aisle, Rack = l.Rack, Shelf = l.Shelf, Bin = l.Bin, LocationType = l.LocationType,
                Capacity = l.Capacity, IsActive = l.IsActive, Notes = l.Notes, Used = used,
                Utilization = l.Capacity.HasValue && l.Capacity.Value > 0 ? Math.Round(used / l.Capacity.Value * 100, 1) : null,
                Items = items == null
                    ? new List<LocationItemDto>()
                    : items.Select(i => new LocationItemDto
                    {
                        ProductId = i.ProductId, ProductName = i.ProductName, ProductNumber = i.ProductNumber, Quantity = i.Qty
                    }).OrderByDescending(i => i.Quantity).ToList()
            };
        }).OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToList();

        var withCap = list.Where(x => x.Capacity.HasValue).ToList();
        var zones = list.GroupBy(x => string.IsNullOrWhiteSpace(x.Zone) ? "-" : x.Zone!).OrderBy(g => g.Key).Select(g =>
        {
            var cap = g.Where(x => x.Capacity.HasValue).Sum(x => x.Capacity!.Value);
            var used = g.Where(x => x.Capacity.HasValue).Sum(x => x.Used);
            return new ZoneSummaryDto
            {
                Zone = g.Key, Locations = g.Count(), Empty = g.Count(x => x.Used <= 0), Capacity = cap, Used = used,
                Percent = cap > 0 ? Math.Round(used / cap * 100, 1) : 0
            };
        }).ToList();

        // Stock that is on hand in the warehouse but not yet put away in any location.
        List<UnplacedItemDto>? unplaced = null;
        if (warehouseId != null)
        {
            var onHand = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(x => x.WarehouseId == warehouseId && x.Status == InventoryTransactionStatus.Confirmed && x.Product!.Physical == true)
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
                .ToListAsync(ct);
            var placed = stocks.Where(s => s.ProductId != null).GroupBy(s => s.ProductId!).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
            var ids = onHand.Where(o => o.ProductId != null && (o.Stock ?? 0) > 0).Select(o => o.ProductId!).ToList();
            var names = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Number }).ToListAsync(ct);
            var nameById = names.ToDictionary(p => p.Id);
            unplaced = onHand.Where(o => o.ProductId != null && nameById.ContainsKey(o.ProductId!))
                .Select(o =>
                {
                    var pl = placed.TryGetValue(o.ProductId!, out var v) ? v : 0;
                    var oh = o.Stock ?? 0;
                    return new UnplacedItemDto
                    {
                        ProductId = o.ProductId, ProductName = nameById[o.ProductId!].Name, ProductNumber = nameById[o.ProductId!].Number,
                        OnHand = oh, Placed = pl, Unplaced = Math.Max(oh - pl, 0)
                    };
                })
                .Where(x => x.Unplaced > 0).OrderByDescending(x => x.Unplaced).Take(100).ToList();
        }

        var totalCap = withCap.Sum(x => x.Capacity!.Value);
        var totalUsedCap = withCap.Sum(x => x.Used);
        var summary = new StorageLocationSummaryDto
        {
            TotalLocations = list.Count,
            ActiveLocations = list.Count(x => x.IsActive),
            EmptyLocations = list.Count(x => x.IsActive && x.Used <= 0),
            FullLocations = list.Count(x => x.Utilization.HasValue && x.Utilization.Value >= 95),
            TotalCapacity = totalCap,
            TotalUsed = totalUsedCap,
            UtilizationPercent = totalCap > 0 ? Math.Round(totalUsedCap / totalCap * 100, 1) : 0,
            UnplacedUnits = unplaced?.Sum(x => x.Unplaced) ?? 0
        };

        return new GetStorageLocationListResult { Data = list, Summary = summary, Zones = zones, Unplaced = unplaced };
    }
}
