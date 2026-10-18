using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.BackorderManager.Queries;

public class BackorderLineDto
{
    public string? SalesOrderId { get; init; }
    public string? OrderNumber { get; init; }
    public DateTime? OrderDate { get; init; }
    public int DaysWaiting { get; init; }
    public string? Customer { get; init; }
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Ordered { get; init; }
    public double Delivered { get; init; }
    public double Outstanding { get; init; }
    /// <summary>What current stock can cover, oldest orders first.</summary>
    public double CanShipNow { get; init; }
    public double Short { get; init; }
    /// <summary>Part of the shortage that open purchase orders will cover.</summary>
    public double CoveredByIncoming { get; init; }
    /// <summary>Ready (fully coverable now), Partial or Backordered (nothing can ship now).</summary>
    public string? State { get; init; }
}

public class BackorderProductDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double OnHand { get; init; }
    public double Outstanding { get; init; }
    public double Short { get; init; }
    public double Incoming { get; init; }
    /// <summary>Units still to be ordered from vendors once incoming is counted.</summary>
    public double ToOrder { get; init; }
}

public class GetBackordersResult
{
    public List<BackorderLineDto> Lines { get; init; } = new();
    public List<BackorderProductDto> Products { get; init; } = new();
    public int OrdersAffected { get; init; }
    public int LinesBackordered { get; init; }
    public double UnitsShort { get; init; }
    public double OldestDays { get; init; }
}

public class GetBackordersRequest : IRequest<GetBackordersResult>
{
}

public class GetBackordersHandler : IRequestHandler<GetBackordersRequest, GetBackordersResult>
{
    private readonly IQueryContext _context;

    public GetBackordersHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetBackordersResult> Handle(GetBackordersRequest request, CancellationToken ct)
    {
        var soLines = await _context.SalesOrderItem.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(i => i.SalesOrder!.IsDeleted == false && i.SalesOrder.OrderStatus == SalesOrderStatus.Confirmed && i.Product!.Physical == true)
            .Select(i => new
            {
                i.SalesOrderId, i.ProductId, Qty = i.Quantity ?? 0,
                OrderNumber = i.SalesOrder!.Number, i.SalesOrder.OrderDate, Customer = i.SalesOrder.Customer!.Name,
                ProductNumber = i.Product!.Number, ProductName = i.Product.Name
            })
            .ToListAsync(ct);
        var soIds = soLines.Select(l => l.SalesOrderId).Distinct().ToList();

        var deliveries = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status == DeliveryOrderStatus.Confirmed && soIds.Contains(d.SalesOrderId))
            .Select(d => new { d.Id, d.SalesOrderId }).ToListAsync(ct);
        var deliveryToSo = deliveries.ToDictionary(d => d.Id, d => d.SalesOrderId);
        var deliveredTx = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(DeliveryOrder) && x.Status == InventoryTransactionStatus.Confirmed)
            .Select(x => new { x.ModuleId, x.ProductId, Qty = Math.Abs(x.Movement ?? 0) }).ToListAsync(ct);
        var delivered = deliveredTx
            .Where(x => x.ModuleId != null && deliveryToSo.ContainsKey(x.ModuleId))
            .GroupBy(x => (deliveryToSo[x.ModuleId!], x.ProductId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        var stockRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) }).ToListAsync(ct);
        var pool = stockRows.Where(r => r.ProductId != null).ToDictionary(r => r.ProductId!, r => Math.Max(r.Stock ?? 0.0, 0));
        var onHand = new Dictionary<string, double>(pool);

        // ---- incoming from open purchase orders ----
        var poLines = await _context.PurchaseOrderItem.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(i => i.PurchaseOrder!.IsDeleted == false && i.PurchaseOrder.OrderStatus == PurchaseOrderStatus.Confirmed)
            .Select(i => new { i.PurchaseOrderId, i.ProductId, Qty = i.Quantity ?? 0 }).ToListAsync(ct);
        var poIds = poLines.Select(l => l.PurchaseOrderId).Distinct().ToList();
        var receipts = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => d.Status == GoodsReceiveStatus.Confirmed && poIds.Contains(d.PurchaseOrderId))
            .Select(d => new { d.Id, d.PurchaseOrderId }).ToListAsync(ct);
        var receiptToPo = receipts.ToDictionary(d => d.Id, d => d.PurchaseOrderId);
        var receivedTx = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(GoodsReceive) && x.Status == InventoryTransactionStatus.Confirmed)
            .Select(x => new { x.ModuleId, x.ProductId, Qty = Math.Abs(x.Movement ?? 0) }).ToListAsync(ct);
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
        var incomingPool = new Dictionary<string, double>(incoming);

        // ---- allocate stock, then incoming, to outstanding lines, oldest order first ----
        var today = DateTime.UtcNow.Date;
        var lines = new List<BackorderLineDto>();
        var grouped = soLines.GroupBy(l => (l.SalesOrderId, l.ProductId, l.OrderNumber, l.OrderDate, l.Customer, l.ProductNumber, l.ProductName))
            .Select(g => new { g.Key, Ordered = g.Sum(l => l.Qty) })
            .OrderBy(x => x.Key.OrderDate).ThenBy(x => x.Key.OrderNumber);
        foreach (var x in grouped)
        {
            var pid = x.Key.ProductId ?? string.Empty;
            delivered.TryGetValue((x.Key.SalesOrderId, x.Key.ProductId), out var done);
            var outstanding = Math.Max(x.Ordered - done, 0);
            if (outstanding <= 0) continue;

            pool.TryGetValue(pid, out var avail);
            var ship = Math.Min(outstanding, avail);
            pool[pid] = avail - ship;
            var shortQty = outstanding - ship;

            incomingPool.TryGetValue(pid, out var inc);
            var covered = Math.Min(shortQty, inc);
            incomingPool[pid] = inc - covered;

            lines.Add(new BackorderLineDto
            {
                SalesOrderId = x.Key.SalesOrderId, OrderNumber = x.Key.OrderNumber, OrderDate = x.Key.OrderDate,
                DaysWaiting = x.Key.OrderDate.HasValue ? Math.Max((int)(today - x.Key.OrderDate.Value.Date).TotalDays, 0) : 0,
                Customer = x.Key.Customer, ProductId = pid, ProductNumber = x.Key.ProductNumber, ProductName = x.Key.ProductName,
                Ordered = x.Ordered, Delivered = done, Outstanding = outstanding, CanShipNow = ship, Short = shortQty, CoveredByIncoming = covered,
                State = shortQty <= 1e-9 ? "Ready" : ship > 0 ? "Partial" : "Backordered"
            });
        }

        var products = lines.GroupBy(l => l.ProductId)
            .Select(g =>
            {
                var shortSum = g.Sum(l => l.Short);
                var inc = incoming.TryGetValue(g.Key ?? "", out var i) ? i : 0;
                return new BackorderProductDto
                {
                    ProductId = g.Key, ProductNumber = g.First().ProductNumber, ProductName = g.First().ProductName,
                    OnHand = onHand.TryGetValue(g.Key ?? "", out var oh) ? oh : 0,
                    Outstanding = g.Sum(l => l.Outstanding), Short = shortSum, Incoming = inc, ToOrder = Math.Max(shortSum - inc, 0)
                };
            })
            .Where(p => p.Short > 0)
            .OrderByDescending(p => p.ToOrder).ThenByDescending(p => p.Short)
            .ToList();

        var short1 = lines.Where(l => l.Short > 0).ToList();
        return new GetBackordersResult
        {
            Lines = lines.OrderByDescending(l => l.State == "Backordered").ThenByDescending(l => l.State == "Partial").ThenByDescending(l => l.DaysWaiting).ToList(),
            Products = products,
            OrdersAffected = short1.Select(l => l.SalesOrderId).Distinct().Count(),
            LinesBackordered = short1.Count,
            UnitsShort = short1.Sum(l => l.Short),
            OldestDays = short1.Count == 0 ? 0 : short1.Max(l => l.DaysWaiting)
        };
    }
}
