using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReceivingManager.Queries;

public class ReceiptDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public DateTime? ReceiveDate { get; init; }
    public string? Vendor { get; init; }
    public double Received { get; init; }
    public double PutAway { get; init; }
    public double Holdable { get; init; }
    public bool Inspected { get; init; }
}

public class PutAwayLocationDto
{
    public string? LocationId { get; init; }
    public string? Code { get; init; }
    public string? Zone { get; init; }
    /// <summary>Units of this product already kept here.</summary>
    public double HoldsProduct { get; init; }
    /// <summary>Room left, or null when the location has no capacity limit.</summary>
    public double? Free { get; init; }
    public double Suggested { get; init; }
}

public class ReceiptLineDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Received { get; init; }
    public bool Inspected { get; init; }
    public double Passed { get; init; }
    public double Failed { get; init; }
    public string? Result { get; init; }
    public string? Reason { get; init; }
    public double PutAway { get; init; }
    public double Remaining { get; init; }
    public List<PutAwayLocationDto> Locations { get; init; } = new();
}

public class PutAwayHistoryDto
{
    public DateTime? At { get; init; }
    public string? ProductName { get; init; }
    public string? LocationCode { get; init; }
    public double Quantity { get; init; }
    public string? UserId { get; init; }
}

public class GetReceivingResult
{
    public List<ReceiptDto> Receipts { get; init; } = new();
    public string? WarehouseName { get; init; }
    public List<ReceiptLineDto> Lines { get; init; } = new();
    public List<PutAwayHistoryDto> History { get; init; } = new();
}

public class GetReceivingRequest : IRequest<GetReceivingResult>
{
    public string? GoodsReceiveId { get; init; }
}

public class GetReceivingHandler : IRequestHandler<GetReceivingRequest, GetReceivingResult>
{
    private readonly IQueryContext _context;

    public GetReceivingHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetReceivingResult> Handle(GetReceivingRequest request, CancellationToken ct)
    {
        var receipts = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status == GoodsReceiveStatus.Confirmed)
            .OrderByDescending(d => d.ReceiveDate)
            .Take(200)
            .Select(d => new { d.Id, d.Number, d.ReceiveDate, Vendor = d.PurchaseOrder!.Vendor!.Name })
            .ToListAsync(ct);
        var ids = receipts.Select(r => r.Id).ToList();

        var txRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(Domain.Entities.GoodsReceive) && ids.Contains(x.ModuleId))
            .Select(x => new { x.ModuleId, x.ProductId, x.WarehouseId, Qty = x.Movement ?? 0 })
            .ToListAsync(ct);
        var inspections = await _context.QualityInspection.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => ids.Contains(x.GoodsReceiveId))
            .Select(x => new { x.GoodsReceiveId, x.ProductId, Passed = x.PassedQuantity ?? 0, Failed = x.FailedQuantity ?? 0, x.Result, x.Reason })
            .ToListAsync(ct);
        var puts = await _context.PutAwayRecord.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => ids.Contains(x.GoodsReceiveId))
            .Select(x => new { x.GoodsReceiveId, x.ProductId, x.LocationId, x.CreatedAtUtc, x.CreatedById, Qty = x.Quantity ?? 0 })
            .ToListAsync(ct);

        double Holdable(string rid) => txRows.Where(t => t.ModuleId == rid).GroupBy(t => t.ProductId).Sum(g =>
        {
            var got = g.Sum(t => Math.Abs(t.Qty));
            var i = inspections.FirstOrDefault(x => x.GoodsReceiveId == rid && x.ProductId == g.Key);
            return i != null ? Math.Min(got, i.Passed) : got;
        });

        var result = new GetReceivingResult
        {
            Receipts = receipts.Select(r => new ReceiptDto
            {
                Id = r.Id, Number = r.Number, ReceiveDate = r.ReceiveDate, Vendor = r.Vendor,
                Received = txRows.Where(t => t.ModuleId == r.Id).Sum(t => Math.Abs(t.Qty)),
                PutAway = puts.Where(p => p.GoodsReceiveId == r.Id).Sum(p => p.Qty),
                Holdable = Holdable(r.Id),
                Inspected = inspections.Any(i => i.GoodsReceiveId == r.Id)
            }).ToList()
        };

        var id = request.GoodsReceiveId;
        if (string.IsNullOrEmpty(id) || !ids.Contains(id)) return result;

        var mine = txRows.Where(x => x.ModuleId == id).ToList();
        var warehouseId = mine.Select(x => x.WarehouseId).FirstOrDefault(w => w != null);
        var warehouseName = warehouseId == null ? null
            : await _context.Warehouse.AsNoTracking().Where(w => w.Id == warehouseId).Select(w => w.Name).FirstOrDefaultAsync(ct);

        var productIds = mine.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Product.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);

        var locations = await _context.StorageLocation.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(l => l.IsActive && (warehouseId == null || l.WarehouseId == warehouseId))
            .Select(l => new { l.Id, l.Code, l.Zone, l.Capacity })
            .ToListAsync(ct);
        var locIds = locations.Select(l => l.Id).ToList();
        var stock = await _context.LocationStock.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => locIds.Contains(s.LocationId))
            .Select(s => new { s.LocationId, s.ProductId, Qty = s.Quantity ?? 0 })
            .ToListAsync(ct);

        // room we have already promised to earlier lines on this screen
        var promised = new Dictionary<string, double>();
        var lines = new List<ReceiptLineDto>();
        foreach (var g in mine.GroupBy(x => x.ProductId))
        {
            var got = g.Sum(x => Math.Abs(x.Qty));
            var insp = inspections.FirstOrDefault(x => x.GoodsReceiveId == id && x.ProductId == g.Key);
            var placed = puts.Where(p => p.GoodsReceiveId == id && p.ProductId == g.Key).Sum(p => p.Qty);
            var allowed = insp != null ? Math.Min(got, insp.Passed) : got;
            var remaining = Math.Max(allowed - placed, 0);
            var left = remaining;

            var options = locations
                .Select(l =>
                {
                    var used = stock.Where(s => s.LocationId == l.Id).Sum(s => s.Qty) + (promised.TryGetValue(l.Id, out var pr) ? pr : 0);
                    var holds = stock.Where(s => s.LocationId == l.Id && s.ProductId == g.Key).Sum(s => s.Qty);
                    double? free = l.Capacity.HasValue ? Math.Max(l.Capacity.Value - used, 0) : null;
                    return new { l, holds, free };
                })
                .Where(o => o.free == null || o.free > 0)
                .OrderByDescending(o => o.holds > 0).ThenBy(o => o.l.Code)
                .Take(6).ToList();

            var locs = new List<PutAwayLocationDto>();
            foreach (var o in options)
            {
                var take = Math.Min(left, o.free ?? left);
                left -= take;
                if (take > 0) promised[o.l.Id] = (promised.TryGetValue(o.l.Id, out var pv) ? pv : 0) + take;
                locs.Add(new PutAwayLocationDto { LocationId = o.l.Id, Code = o.l.Code, Zone = o.l.Zone, HoldsProduct = o.holds, Free = o.free, Suggested = take });
            }

            var p = products.FirstOrDefault(x => x.Id == g.Key);
            lines.Add(new ReceiptLineDto
            {
                ProductId = g.Key, ProductNumber = p?.Number, ProductName = p?.Name, Received = got,
                Inspected = insp != null, Passed = insp?.Passed ?? got, Failed = insp?.Failed ?? 0, Result = insp?.Result, Reason = insp?.Reason,
                PutAway = placed, Remaining = remaining, Locations = locs
            });
        }

        var putLocIds = puts.Select(p => p.LocationId).Distinct().ToList();
        var locCodes = await _context.StorageLocation.AsNoTracking().Where(l => putLocIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Code }).ToListAsync(ct);
        var history = puts.Where(x => x.GoodsReceiveId == id).OrderByDescending(x => x.CreatedAtUtc).Select(x => new PutAwayHistoryDto
        {
            At = x.CreatedAtUtc,
            ProductName = products.FirstOrDefault(p => p.Id == x.ProductId)?.Name,
            LocationCode = locCodes.FirstOrDefault(l => l.Id == x.LocationId)?.Code,
            Quantity = x.Qty, UserId = x.CreatedById
        }).ToList();

        return new GetReceivingResult { Receipts = result.Receipts, WarehouseName = warehouseName, Lines = lines, History = history };
    }
}
