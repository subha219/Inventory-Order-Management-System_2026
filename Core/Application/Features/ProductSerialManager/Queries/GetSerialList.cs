using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ProductSerialManager.Queries;

public class SerialDto
{
    public string? Id { get; init; }
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? SerialNumber { get; init; }
    public string? Status { get; init; }
    public string? WarehouseName { get; init; }
    public string? LocationCode { get; init; }
    public string? GoodsReceiveNumber { get; init; }
    public string? DeliveryOrderId { get; init; }
    public string? DeliveryOrderNumber { get; init; }
    public string? CustomerName { get; init; }
    public DateTime? RegisteredAt { get; init; }
    public DateTime? ShippedAt { get; init; }
    public string? Notes { get; init; }
}

public class SerialProductSummaryDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public int InStock { get; init; }
    public int Shipped { get; init; }
    public double OnHand { get; init; }
    /// <summary>Units on hand that have no serial number yet.</summary>
    public double Unregistered { get; init; }
}

public class DeliveryChoiceDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Customer { get; init; }
    public List<string> ProductIds { get; init; } = new();
}

public class GetSerialListResult
{
    public List<SerialDto> Data { get; init; } = new();
    public List<SerialProductSummaryDto> Summary { get; init; } = new();
    public List<DeliveryChoiceDto> Deliveries { get; init; } = new();
    public int Total { get; init; }
    public bool Truncated { get; init; }
}

public class GetSerialListRequest : IRequest<GetSerialListResult>
{
    public string? Search { get; init; }
    public string? ProductId { get; init; }
    public string? Status { get; init; }
}

public class GetSerialListHandler : IRequestHandler<GetSerialListRequest, GetSerialListResult>
{
    private readonly IQueryContext _context;

    public GetSerialListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetSerialListResult> Handle(GetSerialListRequest request, CancellationToken ct)
    {
        var all = _context.ProductSerial.AsNoTracking().ApplyIsDeletedFilter(false);

        var q = all;
        if (!string.IsNullOrWhiteSpace(request.ProductId)) q = q.Where(x => x.ProductId == request.ProductId);
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(x => x.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            q = q.Where(x => x.SerialNumber!.Contains(s) || x.DeliveryOrderNumber!.Contains(s) || x.CustomerName!.Contains(s) ||
                             x.GoodsReceiveNumber!.Contains(s) || x.Product!.Name!.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).Take(500)
            .Select(x => new SerialDto
            {
                Id = x.Id, ProductId = x.ProductId, ProductNumber = x.Product!.Number, ProductName = x.Product.Name,
                SerialNumber = x.SerialNumber, Status = x.Status, GoodsReceiveNumber = x.GoodsReceiveNumber,
                DeliveryOrderId = x.DeliveryOrderId, DeliveryOrderNumber = x.DeliveryOrderNumber, CustomerName = x.CustomerName,
                RegisteredAt = x.CreatedAtUtc, ShippedAt = x.ShippedAtUtc, Notes = x.Notes
            }).ToListAsync(ct);

        // names for warehouse / location without extra navigation properties
        var raw = await q.OrderByDescending(x => x.CreatedAtUtc).Take(500).Select(x => new { x.Id, x.WarehouseId, x.LocationId }).ToListAsync(ct);
        var whIds = raw.Select(r => r.WarehouseId).Distinct().ToList();
        var locIds = raw.Select(r => r.LocationId).Distinct().ToList();
        var whs = await _context.Warehouse.AsNoTracking().Where(w => whIds.Contains(w.Id)).Select(w => new { w.Id, w.Name }).ToListAsync(ct);
        var locs = await _context.StorageLocation.AsNoTracking().Where(l => locIds.Contains(l.Id)).Select(l => new { l.Id, l.Code }).ToListAsync(ct);
        var data = rows.Select(r =>
        {
            var m = raw.First(x => x.Id == r.Id);
            return new SerialDto
            {
                Id = r.Id, ProductId = r.ProductId, ProductNumber = r.ProductNumber, ProductName = r.ProductName, SerialNumber = r.SerialNumber,
                Status = r.Status, GoodsReceiveNumber = r.GoodsReceiveNumber, DeliveryOrderId = r.DeliveryOrderId, DeliveryOrderNumber = r.DeliveryOrderNumber,
                CustomerName = r.CustomerName, RegisteredAt = r.RegisteredAt, ShippedAt = r.ShippedAt, Notes = r.Notes,
                WarehouseName = whs.FirstOrDefault(w => w.Id == m.WarehouseId)?.Name, LocationCode = locs.FirstOrDefault(l => l.Id == m.LocationId)?.Code
            };
        }).ToList();

        // ---- per product: serials vs units on hand ----
        var counts = await all.GroupBy(x => new { x.ProductId, x.Status }).Select(g => new { g.Key.ProductId, g.Key.Status, N = g.Count() }).ToListAsync(ct);
        var stockRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false)
            .GroupBy(x => x.ProductId).Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) }).ToListAsync(ct);
        var onHand = stockRows.Where(r => r.ProductId != null).ToDictionary(r => r.ProductId!, r => r.Stock ?? 0.0);
        var pids = counts.Select(c => c.ProductId).Distinct().ToList();
        var prods = await _context.Product.AsNoTracking().Where(p => pids.Contains(p.Id)).Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);
        var summary = pids.Select(pid =>
        {
            var p = prods.FirstOrDefault(x => x.Id == pid);
            var inStock = counts.Where(c => c.ProductId == pid && (c.Status == "InStock" || c.Status == "Returned")).Sum(c => c.N);
            var oh = pid != null && onHand.TryGetValue(pid, out var v) ? v : 0;
            return new SerialProductSummaryDto
            {
                ProductId = pid, ProductNumber = p?.Number, ProductName = p?.Name, InStock = inStock,
                Shipped = counts.Where(c => c.ProductId == pid && c.Status == "Shipped").Sum(c => c.N), OnHand = oh, Unregistered = Math.Max(oh - inStock, 0)
            };
        }).OrderBy(s => s.ProductName).ToList();

        // ---- deliveries a unit can be shipped on ----
        var deliveries = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status == DeliveryOrderStatus.Draft || d.Status == DeliveryOrderStatus.Confirmed)
            .OrderByDescending(d => d.DeliveryDate).Take(150)
            .Select(d => new { d.Id, d.Number, Customer = d.SalesOrder!.Customer!.Name }).ToListAsync(ct);
        var dIds = deliveries.Select(d => d.Id).ToList();
        var dTx = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(Domain.Entities.DeliveryOrder) && dIds.Contains(x.ModuleId))
            .Select(x => new { x.ModuleId, x.ProductId }).ToListAsync(ct);

        return new GetSerialListResult
        {
            Data = data, Summary = summary, Total = total, Truncated = total > 500,
            Deliveries = deliveries.Select(d => new DeliveryChoiceDto
            {
                Id = d.Id, Number = d.Number, Customer = d.Customer,
                ProductIds = dTx.Where(t => t.ModuleId == d.Id).Select(t => t.ProductId ?? "").Distinct().ToList()
            }).ToList()
        };
    }
}
