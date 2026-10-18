using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ProductManager.Queries;

public class StockAvailabilityDto
{
    public string? ProductId { get; init; }
    public string? Number { get; init; }
    public string? Name { get; init; }
    public double OnHand { get; init; }
    /// <summary>Quantity promised on confirmed sales orders that has not been delivered yet.</summary>
    public double Reserved { get; init; }
    /// <summary>Units set aside by a stock hold.</summary>
    public double Held { get; init; }
    public double Available { get; init; }
    /// <summary>Quantity on confirmed purchase orders that has not been received yet.</summary>
    public double Incoming { get; init; }
    /// <summary>Available plus incoming.</summary>
    public double Projected { get; init; }
    public double? ReorderPoint { get; init; }
    /// <summary>Ok, Low, Short or Overcommitted.</summary>
    public string? State { get; init; }
}

public class GetStockAvailabilityResult
{
    public List<StockAvailabilityDto>? Data { get; init; }
    public int Products { get; init; }
    public int Overcommitted { get; init; }
    public int Low { get; init; }
    public double TotalReserved { get; init; }
    public double TotalIncoming { get; init; }
}

public class GetStockAvailabilityRequest : IRequest<GetStockAvailabilityResult>
{
}

public class GetStockAvailabilityHandler : IRequestHandler<GetStockAvailabilityRequest, GetStockAvailabilityResult>
{
    private readonly IQueryContext _context;

    public GetStockAvailabilityHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetStockAvailabilityResult> Handle(GetStockAvailabilityRequest request, CancellationToken ct)
    {
        var products = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(p => p.Physical == true)
            .Select(p => new { p.Id, p.Number, p.Name, p.ReorderPoint })
            .ToListAsync(ct);

        var stockRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(ct);
        var onHand = stockRows.Where(r => r.ProductId != null).ToDictionary(r => r.ProductId!, r => r.Stock ?? 0.0);

        // ---- reserved: confirmed sales order lines minus what confirmed deliveries already took out ----
        var soLines = await _context.SalesOrderItem.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(i => i.SalesOrder!.IsDeleted == false && i.SalesOrder.OrderStatus == SalesOrderStatus.Confirmed)
            .Select(i => new { i.SalesOrderId, i.ProductId, Qty = i.Quantity ?? 0 })
            .ToListAsync(ct);
        var soIds = soLines.Select(l => l.SalesOrderId).Distinct().ToList();
        var deliveries = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status == DeliveryOrderStatus.Confirmed && soIds.Contains(d.SalesOrderId))
            .Select(d => new { d.Id, d.SalesOrderId })
            .ToListAsync(ct);
        var deliveryToSo = deliveries.ToDictionary(d => d.Id, d => d.SalesOrderId);
        var deliveredTx = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(DeliveryOrder) && x.Status == InventoryTransactionStatus.Confirmed)
            .Select(x => new { x.ModuleId, x.ProductId, Qty = Math.Abs(x.Movement ?? 0) })
            .ToListAsync(ct);
        var delivered = deliveredTx
            .Where(x => x.ModuleId != null && deliveryToSo.ContainsKey(x.ModuleId))
            .GroupBy(x => (deliveryToSo[x.ModuleId!], x.ProductId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
        var reserved = new Dictionary<string, double>();
        foreach (var g in soLines.GroupBy(l => (l.SalesOrderId, l.ProductId)))
        {
            if (g.Key.ProductId == null) continue;
            delivered.TryGetValue((g.Key.SalesOrderId, g.Key.ProductId), out var done);
            var open = Math.Max(g.Sum(l => l.Qty) - done, 0);
            reserved[g.Key.ProductId] = reserved.TryGetValue(g.Key.ProductId, out var r) ? r + open : open;
        }

        // ---- incoming: confirmed purchase order lines minus what confirmed receipts already brought in ----
        var poLines = await _context.PurchaseOrderItem.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(i => i.PurchaseOrder!.IsDeleted == false && i.PurchaseOrder.OrderStatus == PurchaseOrderStatus.Confirmed)
            .Select(i => new { i.PurchaseOrderId, i.ProductId, Qty = i.Quantity ?? 0 })
            .ToListAsync(ct);
        var poIds = poLines.Select(l => l.PurchaseOrderId).Distinct().ToList();
        var receipts = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status == GoodsReceiveStatus.Confirmed && poIds.Contains(d.PurchaseOrderId))
            .Select(d => new { d.Id, d.PurchaseOrderId })
            .ToListAsync(ct);
        var receiptToPo = receipts.ToDictionary(d => d.Id, d => d.PurchaseOrderId);
        var receivedTx = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(GoodsReceive) && x.Status == InventoryTransactionStatus.Confirmed)
            .Select(x => new { x.ModuleId, x.ProductId, Qty = Math.Abs(x.Movement ?? 0) })
            .ToListAsync(ct);
        var received = receivedTx
            .Where(x => x.ModuleId != null && receiptToPo.ContainsKey(x.ModuleId))
            .GroupBy(x => (receiptToPo[x.ModuleId!], x.ProductId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
        var incoming = new Dictionary<string, double>();
        foreach (var g in poLines.GroupBy(l => (l.PurchaseOrderId, l.ProductId)))
        {
            if (g.Key.ProductId == null) continue;
            received.TryGetValue((g.Key.PurchaseOrderId, g.Key.ProductId), out var done);
            var open = Math.Max(g.Sum(l => l.Qty) - done, 0);
            incoming[g.Key.ProductId] = incoming.TryGetValue(g.Key.ProductId, out var r) ? r + open : open;
        }

        var heldRows = await _context.StockHold.AsNoTracking().ApplyIsDeletedFilter(false).Where(h => h.Status == "Active")
            .GroupBy(h => h.ProductId).Select(g => new { ProductId = g.Key, Qty = g.Sum(h => h.Quantity) }).ToListAsync(ct);
        var held = heldRows.Where(h => h.ProductId != null).ToDictionary(h => h.ProductId!, h => h.Qty ?? 0.0);

        var rows = new List<StockAvailabilityDto>();
        foreach (var p in products)
        {
            var oh = onHand.TryGetValue(p.Id, out var o) ? o : 0.0;
            var rs = reserved.TryGetValue(p.Id, out var r1) ? r1 : 0.0;
            var inc = incoming.TryGetValue(p.Id, out var i1) ? i1 : 0.0;
            if (oh == 0 && rs == 0 && inc == 0) continue;
            var hd = held.TryGetValue(p.Id, out var hh) ? hh : 0.0;
            var avail = oh - rs - hd;
            var state = avail < 0 ? "Overcommitted"
                : (p.ReorderPoint ?? 0) > 0 && avail <= p.ReorderPoint ? "Low"
                : avail == 0 && rs > 0 ? "Short" : "Ok";
            rows.Add(new StockAvailabilityDto
            {
                ProductId = p.Id, Number = p.Number, Name = p.Name, OnHand = oh, Reserved = rs, Held = hd, Available = avail,
                Incoming = inc, Projected = avail + inc, ReorderPoint = p.ReorderPoint, State = state
            });
        }

        return new GetStockAvailabilityResult
        {
            Data = rows.OrderBy(x => x.Available).ThenBy(x => x.Name).ToList(),
            Products = rows.Count,
            Overcommitted = rows.Count(x => x.State == "Overcommitted"),
            Low = rows.Count(x => x.State == "Low" || x.State == "Short"),
            TotalReserved = rows.Sum(x => x.Reserved),
            TotalIncoming = rows.Sum(x => x.Incoming)
        };
    }
}
