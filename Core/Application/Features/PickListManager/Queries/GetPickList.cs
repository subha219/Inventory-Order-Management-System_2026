using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.PickListManager.Queries;

public class PickOrderDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public string? Customer { get; init; }
    public int Status { get; init; }
    public double Needed { get; init; }
    public double Picked { get; init; }
}

public class PickSourceDto
{
    public string? LocationId { get; init; }
    public string? Code { get; init; }
    public string? Zone { get; init; }
    public double Quantity { get; init; }
    /// <summary>Quantity the screen suggests taking from this location.</summary>
    public double Suggested { get; init; }
}

public class PickBatchDto
{
    public string? BatchId { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public double Quantity { get; init; }
    /// <summary>First-expiry-first-out suggestion; expired batches are never suggested.</summary>
    public double Suggested { get; init; }
    public bool Expired { get; init; }
    public int? DaysToExpiry { get; init; }
}

public class PickLineDto
{
    public List<PickBatchDto> Batches { get; init; } = new();
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Needed { get; init; }
    public double Picked { get; init; }
    public double Remaining { get; init; }
    /// <summary>Units of this product in the warehouse that have not been put into any location yet.</summary>
    public double Unplaced { get; init; }
    public List<PickSourceDto> Sources { get; init; } = new();
}

public class PickHistoryDto
{
    public DateTime? At { get; init; }
    public string? ProductName { get; init; }
    public string? LocationCode { get; init; }
    public double Quantity { get; init; }
    public string? UserId { get; init; }
}

public class GetPickListResult
{
    public List<PickOrderDto> Orders { get; init; } = new();
    public string? WarehouseName { get; init; }
    public List<PickLineDto> Lines { get; init; } = new();
    public List<PickHistoryDto> History { get; init; } = new();
}

public class GetPickListRequest : IRequest<GetPickListResult>
{
    /// <summary>Delivery order to show the pick lines for; leave empty to list orders only.</summary>
    public string? DeliveryOrderId { get; init; }
}

public class GetPickListHandler : IRequestHandler<GetPickListRequest, GetPickListResult>
{
    private readonly IQueryContext _context;

    public GetPickListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetPickListResult> Handle(GetPickListRequest request, CancellationToken ct)
    {
        var orders = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status == DeliveryOrderStatus.Draft || d.Status == DeliveryOrderStatus.Confirmed)
            .OrderByDescending(d => d.DeliveryDate)
            .Take(200)
            .Select(d => new { d.Id, d.Number, d.DeliveryDate, Customer = d.SalesOrder!.Customer!.Name, Status = (int)d.Status! })
            .ToListAsync(ct);
        var ids = orders.Select(o => o.Id).ToList();

        var needRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(Domain.Entities.DeliveryOrder) && ids.Contains(x.ModuleId))
            .Select(x => new { x.ModuleId, x.ProductId, x.WarehouseId, Qty = x.Movement ?? 0 })
            .ToListAsync(ct);
        var pickRows = await _context.DeliveryPick.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => ids.Contains(x.DeliveryOrderId))
            .Select(x => new { x.DeliveryOrderId, x.ProductId, x.LocationId, x.CreatedAtUtc, x.CreatedById, Qty = x.Quantity ?? 0 })
            .ToListAsync(ct);

        var needBy = needRows.GroupBy(x => x.ModuleId).ToDictionary(g => g.Key ?? "", g => g.Sum(x => Math.Abs(x.Qty)));
        var pickedBy = pickRows.GroupBy(x => x.DeliveryOrderId).ToDictionary(g => g.Key ?? "", g => g.Sum(x => x.Qty));

        var result = new GetPickListResult
        {
            Orders = orders.Select(o => new PickOrderDto
            {
                Id = o.Id, Number = o.Number, DeliveryDate = o.DeliveryDate, Customer = o.Customer, Status = o.Status,
                Needed = needBy.TryGetValue(o.Id, out var n) ? n : 0,
                Picked = pickedBy.TryGetValue(o.Id, out var p) ? p : 0
            }).ToList()
        };

        if (string.IsNullOrEmpty(request.DeliveryOrderId) || !ids.Contains(request.DeliveryOrderId)) return result;

        var id = request.DeliveryOrderId;
        var mine = needRows.Where(x => x.ModuleId == id).ToList();
        var warehouseId = mine.Select(x => x.WarehouseId).FirstOrDefault(w => w != null);
        var warehouseName = warehouseId == null ? null
            : await _context.Warehouse.AsNoTracking().Where(w => w.Id == warehouseId).Select(w => w.Name).FirstOrDefaultAsync(ct);

        var productIds = mine.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Product.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);

        var stockRows = await _context.LocationStock.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => productIds.Contains(s.ProductId) && s.Location!.IsDeleted == false && s.Location.IsActive &&
                        (warehouseId == null || s.Location.WarehouseId == warehouseId) && (s.Quantity ?? 0) > 0)
            .Select(s => new { s.LocationId, s.ProductId, Code = s.Location!.Code, Zone = s.Location.Zone, Qty = s.Quantity ?? 0 })
            .ToListAsync(ct);

        var onHandRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && productIds.Contains(x.ProductId) &&
                        (warehouseId == null || x.WarehouseId == warehouseId))
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(ct);
        var onHand = onHandRows.ToDictionary(r => r.ProductId ?? "", r => r.Stock ?? 0.0);

        // units on hold (quality check, damage, recall) are not offered for picking
        var heldRows = await _context.StockHold.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(h => h.Status == "Active" && productIds.Contains(h.ProductId) && (warehouseId == null || h.WarehouseId == warehouseId))
            .GroupBy(h => h.ProductId).Select(g => new { ProductId = g.Key, Qty = g.Sum(h => h.Quantity) }).ToListAsync(ct);
        var heldByProduct = heldRows.Where(h => h.ProductId != null).ToDictionary(h => h.ProductId!, h => h.Qty ?? 0.0);

        var batchRows = await _context.ProductBatch.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(b => productIds.Contains(b.ProductId) && (warehouseId == null || b.WarehouseId == warehouseId) && (b.Quantity ?? 0) > 0)
            .Select(b => new { b.Id, b.ProductId, b.BatchNumber, b.ExpiryDate, Qty = b.Quantity ?? 0 })
            .ToListAsync(ct);
        var today = DateTime.UtcNow.Date;

        var lines = new List<PickLineDto>();
        foreach (var g in mine.GroupBy(x => x.ProductId))
        {
            var need = g.Sum(x => Math.Abs(x.Qty));
            var done = pickRows.Where(x => x.DeliveryOrderId == id && x.ProductId == g.Key).Sum(x => x.Qty);
            var remaining = Math.Max(need - done, 0);
            var left = remaining;
            var sources = new List<PickSourceDto>();
            // suggest locations zone by zone, then by code, so a picker walks each zone once; the picker can change the numbers
            var heldQty = heldByProduct.TryGetValue(g.Key ?? "", out var hq) ? hq : 0.0;
            var route = stockRows.Where(s => s.ProductId == g.Key).OrderBy(s => s.Zone ?? "~").ThenBy(s => s.Code)
                .Select(s => (LocationId: s.LocationId, Code: s.Code, Zone: s.Zone, Qty: s.Qty)).ToList();
            // held stock is taken off the end of the route, so the picker is never sent to it
            for (var k = route.Count - 1; k >= 0 && heldQty > 0; k--)
            {
                var cut = Math.Min(route[k].Qty, heldQty);
                route[k] = (route[k].LocationId, route[k].Code, route[k].Zone, route[k].Qty - cut);
                heldQty -= cut;
            }
            foreach (var s in route.Where(x => x.Qty > 0))
            {
                var take = Math.Min(left, s.Qty);
                left -= take;
                sources.Add(new PickSourceDto { LocationId = s.LocationId, Code = s.Code, Zone = s.Zone, Quantity = s.Qty, Suggested = take });
            }
            var placed = stockRows.Where(s => s.ProductId == g.Key).Sum(s => s.Qty);

            // FEFO: the batch that expires first goes first; expired stock is shown but never suggested
            var fefoLeft = remaining;
            var batches = new List<PickBatchDto>();
            foreach (var b in batchRows.Where(b => b.ProductId == g.Key).OrderBy(b => b.ExpiryDate == null).ThenBy(b => b.ExpiryDate).ThenBy(b => b.BatchNumber))
            {
                var expired = b.ExpiryDate.HasValue && b.ExpiryDate.Value.Date < today;
                var take = expired ? 0 : Math.Min(fefoLeft, b.Qty);
                fefoLeft -= take;
                batches.Add(new PickBatchDto
                {
                    BatchId = b.Id, BatchNumber = b.BatchNumber, ExpiryDate = b.ExpiryDate, Quantity = b.Qty, Suggested = take, Expired = expired,
                    DaysToExpiry = b.ExpiryDate.HasValue ? (int)(b.ExpiryDate.Value.Date - today).TotalDays : null
                });
            }
            var p = products.FirstOrDefault(x => x.Id == g.Key);
            lines.Add(new PickLineDto
            {
                ProductId = g.Key, ProductNumber = p?.Number, ProductName = p?.Name, Needed = need, Picked = done, Remaining = remaining,
                Unplaced = Math.Max((onHand.TryGetValue(g.Key ?? "", out var oh) ? oh : 0) - placed, 0), Sources = sources, Batches = batches
            });
        }

        var locIds = pickRows.Select(p => p.LocationId).Distinct().ToList();
        var locCodes = await _context.StorageLocation.AsNoTracking()
            .Where(l => locIds.Contains(l.Id)).Select(l => new { l.Id, l.Code }).ToListAsync(ct);
        var history = pickRows.Where(x => x.DeliveryOrderId == id).OrderByDescending(x => x.CreatedAtUtc).Select(x => new PickHistoryDto
        {
            At = x.CreatedAtUtc,
            ProductName = products.FirstOrDefault(p => p.Id == x.ProductId)?.Name,
            LocationCode = locCodes.FirstOrDefault(l => l.Id == x.LocationId)?.Code,
            Quantity = x.Qty, UserId = x.CreatedById
        }).ToList();

        return new GetPickListResult { Orders = result.Orders, WarehouseName = warehouseName, Lines = lines, History = history };
    }
}
