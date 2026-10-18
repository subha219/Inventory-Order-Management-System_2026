using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.IntegrationManager.Queries;

public class TrackingItemDto
{
    public string? ProductName { get; init; }
    public double Quantity { get; init; }
}

public class TrackingResult
{
    public bool Found { get; init; }
    public string? ShipmentNumber { get; init; }
    public string? OrderNumber { get; init; }
    public string? Status { get; init; }
    public string? Carrier { get; init; }
    public string? TrackingNumber { get; init; }
    public DateTime? ShippedAtUtc { get; init; }
    public int Packages { get; init; }
    public List<TrackingItemDto> Items { get; init; } = new();
}

public class GetPublicTrackingRequest : IRequest<TrackingResult>
{
    public string? Token { get; init; }
}

public class GetPublicTrackingHandler : IRequestHandler<GetPublicTrackingRequest, TrackingResult>
{
    private readonly IQueryContext _context;

    public GetPublicTrackingHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<TrackingResult> Handle(GetPublicTrackingRequest r, CancellationToken ct)
    {
        var token = r.Token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length < 16 || token.Length > 64) return new TrackingResult { Found = false };
        var s = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).FirstOrDefaultAsync(x => x.PublicToken == token, ct);
        if (s == null) return new TrackingResult { Found = false };
        var order = await _context.DeliveryOrder.AsNoTracking().Where(o => o.Id == s.DeliveryOrderId).Select(o => o.Number).FirstOrDefaultAsync(ct);
        var packages = await _context.ShipmentPackage.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(p => p.ShipmentId == s.Id, ct);
        var items = await _context.ShipmentPackageItem.AsNoTracking().ApplyIsDeletedFilter(false).Where(i => i.ShipmentId == s.Id).ToListAsync(ct);
        var pids = items.Select(i => i.ProductId).Distinct().ToList();
        var names = await _context.Product.AsNoTracking().Where(p => pids.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync(ct);
        var nameOf = names.ToDictionary(p => p.Id, p => p.Name);
        return new TrackingResult
        {
            Found = true, ShipmentNumber = s.Number, OrderNumber = order, Status = s.Status, Carrier = s.Carrier, TrackingNumber = s.TrackingNumber,
            ShippedAtUtc = s.ShippedAtUtc == null ? null : DateTime.SpecifyKind(s.ShippedAtUtc.Value, DateTimeKind.Utc), Packages = packages,
            Items = items.GroupBy(i => i.ProductId).Select(g => new TrackingItemDto { ProductName = nameOf.TryGetValue(g.Key ?? "", out var n) ? n : "Item", Quantity = g.Sum(i => i.Quantity ?? 0) }).ToList()
        };
    }
}

// ---------------------------------------------------------------- read-only integration API
public class StockLevelDto
{
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? Warehouse { get; init; }
    public double OnHand { get; init; }
}

public class GetStockLevelsRequest : IRequest<List<StockLevelDto>>
{
    public string? Sku { get; init; }
    public string? Warehouse { get; init; }
}

public class GetStockLevelsHandler : IRequestHandler<GetStockLevelsRequest, List<StockLevelDto>>
{
    private readonly IQueryContext _context;

    public GetStockLevelsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<List<StockLevelDto>> Handle(GetStockLevelsRequest r, CancellationToken ct)
    {
        var q = _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false);
        if (!string.IsNullOrWhiteSpace(r.Sku)) { var sku = r.Sku.Trim(); q = q.Where(x => x.Product!.Number == sku); }
        if (!string.IsNullOrWhiteSpace(r.Warehouse)) { var w = r.Warehouse.Trim(); q = q.Where(x => x.Warehouse!.Name == w); }
        var rows = await q.GroupBy(x => new { x.ProductId, x.WarehouseId }).Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Stock = g.Sum(x => x.Stock) }).Take(5000).ToListAsync(ct);
        var pids = rows.Select(x => x.ProductId).Distinct().ToList();
        var wids = rows.Select(x => x.WarehouseId).Distinct().ToList();
        var prods = await _context.Product.AsNoTracking().Where(p => pids.Contains(p.Id)).Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);
        var whs = await _context.Warehouse.AsNoTracking().Where(w => wids.Contains(w.Id)).Select(w => new { w.Id, w.Name }).ToListAsync(ct);
        var pd = prods.ToDictionary(p => p.Id); var wd = whs.ToDictionary(w => w.Id, w => w.Name);
        return rows.Where(x => x.ProductId != null && pd.ContainsKey(x.ProductId)).Select(x => new StockLevelDto
        {
            ProductNumber = pd[x.ProductId!].Number, ProductName = pd[x.ProductId!].Name, Warehouse = wd.TryGetValue(x.WarehouseId ?? "", out var wn) ? wn : "", OnHand = x.Stock ?? 0
        }).OrderBy(x => x.ProductNumber).ToList();
    }
}

public class GetShipmentByNumberRequest : IRequest<TrackingResult>
{
    public string? Number { get; init; }
}

public class GetShipmentByNumberHandler : IRequestHandler<GetShipmentByNumberRequest, TrackingResult>
{
    private readonly IQueryContext _context;
    private readonly ISender _sender;

    public GetShipmentByNumberHandler(IQueryContext context, ISender sender)
    {
        _context = context; _sender = sender;
    }

    public async Task<TrackingResult> Handle(GetShipmentByNumberRequest r, CancellationToken ct)
    {
        var n = r.Number?.Trim();
        var s = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).FirstOrDefaultAsync(x => x.Number == n, ct);
        if (s == null) return new TrackingResult { Found = false };
        if (string.IsNullOrEmpty(s.PublicToken)) return new TrackingResult { Found = true, ShipmentNumber = s.Number, Status = s.Status, Carrier = s.Carrier, TrackingNumber = s.TrackingNumber, ShippedAtUtc = s.ShippedAtUtc };
        return await _sender.Send(new GetPublicTrackingRequest { Token = s.PublicToken }, ct);
    }
}
