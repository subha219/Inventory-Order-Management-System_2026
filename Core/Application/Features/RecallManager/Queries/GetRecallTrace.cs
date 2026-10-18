using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.RecallManager.Queries;

public class RecallBatchDto
{
    public string? BatchId { get; init; }
    public string? BatchNumber { get; init; }
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? WarehouseName { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public DateTime? RegisteredAt { get; init; }
    /// <summary>Units of the batch still in the warehouse.</summary>
    public double Remaining { get; init; }
    /// <summary>Units recorded as shipped from this batch.</summary>
    public double Shipped { get; init; }
}

public class RecallShipmentDto
{
    public string? BatchNumber { get; init; }
    public string? SerialNumber { get; init; }
    public string? ProductName { get; init; }
    public string? DeliveryOrderId { get; init; }
    public string? DeliveryOrderNumber { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public string? DeliveryStatus { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public double Quantity { get; init; }
}

public class RecallCustomerDto
{
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public double Quantity { get; init; }
    public int Deliveries { get; init; }
    public DateTime? LastDelivery { get; init; }
}

public class RecallUnlinkedDto
{
    public string? DeliveryOrderNumber { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public string? CustomerName { get; init; }
    public string? ProductName { get; init; }
    public double Quantity { get; init; }
}

public class GetRecallTraceResult
{
    public List<RecallBatchDto> Batches { get; init; } = new();
    public List<RecallShipmentDto> Shipments { get; init; } = new();
    public List<RecallCustomerDto> Customers { get; init; } = new();
    /// <summary>Deliveries of the same product after the batch was registered that have no batch recorded: they may or may not be affected.</summary>
    public List<RecallUnlinkedDto> Unlinked { get; init; } = new();
    public double TotalShipped { get; init; }
    public double TotalRemaining { get; init; }
}

public class GetRecallTraceRequest : IRequest<GetRecallTraceResult>
{
    /// <summary>Batch number or serial number (partial match).</summary>
    public string? Term { get; init; }
}

public class GetRecallTraceHandler : IRequestHandler<GetRecallTraceRequest, GetRecallTraceResult>
{
    private readonly IQueryContext _context;

    public GetRecallTraceHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetRecallTraceResult> Handle(GetRecallTraceRequest request, CancellationToken ct)
    {
        var term = (request.Term ?? string.Empty).Trim();
        if (term.Length < 2) return new GetRecallTraceResult();

        // ---- batches that match ----
        var batchRows = await _context.ProductBatch.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(b => b.BatchNumber!.Contains(term))
            .OrderBy(b => b.BatchNumber).Take(50)
            .Select(b => new { b.Id, b.BatchNumber, b.ProductId, b.WarehouseId, b.ExpiryDate, b.CreatedAtUtc, Qty = b.Quantity ?? 0,
                               ProductNumber = b.Product!.Number, ProductName = b.Product.Name })
            .ToListAsync(ct);
        var batchIds = batchRows.Select(b => b.Id).ToList();
        var whIds = batchRows.Select(b => b.WarehouseId).Distinct().ToList();
        var whs = await _context.Warehouse.AsNoTracking().Where(w => whIds.Contains(w.Id)).Select(w => new { w.Id, w.Name }).ToListAsync(ct);

        var picks = await _context.DeliveryPickBatch.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(p => batchIds.Contains(p.BatchId))
            .Select(p => new { p.BatchId, p.DeliveryOrderId, p.ProductId, Qty = p.Quantity ?? 0 })
            .ToListAsync(ct);

        // ---- serials that match ----
        var serials = await _context.ProductSerial.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => s.SerialNumber!.Contains(term) && s.DeliveryOrderId != null)
            .Take(200)
            .Select(s => new { s.SerialNumber, s.DeliveryOrderId, s.CustomerName, ProductName = s.Product!.Name })
            .ToListAsync(ct);

        // ---- deliveries involved ----
        var dIds = picks.Select(p => p.DeliveryOrderId).Concat(serials.Select(s => s.DeliveryOrderId)).Distinct().ToList();
        var deliveries = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(d => dIds.Contains(d.Id))
            .Select(d => new
            {
                d.Id, d.Number, d.DeliveryDate, d.Status,
                CustomerId = d.SalesOrder!.CustomerId, CustomerName = d.SalesOrder.Customer!.Name
            }).ToListAsync(ct);

        var shipments = new List<RecallShipmentDto>();
        foreach (var p in picks)
        {
            var b = batchRows.First(x => x.Id == p.BatchId);
            var d = deliveries.FirstOrDefault(x => x.Id == p.DeliveryOrderId);
            shipments.Add(new RecallShipmentDto
            {
                BatchNumber = b.BatchNumber, ProductName = b.ProductName, DeliveryOrderId = p.DeliveryOrderId, DeliveryOrderNumber = d?.Number,
                DeliveryDate = d?.DeliveryDate, DeliveryStatus = d?.Status?.ToString(), CustomerId = d?.CustomerId, CustomerName = d?.CustomerName, Quantity = p.Qty
            });
        }
        foreach (var s in serials)
        {
            var d = deliveries.FirstOrDefault(x => x.Id == s.DeliveryOrderId);
            shipments.Add(new RecallShipmentDto
            {
                SerialNumber = s.SerialNumber, ProductName = s.ProductName, DeliveryOrderId = s.DeliveryOrderId, DeliveryOrderNumber = d?.Number,
                DeliveryDate = d?.DeliveryDate, DeliveryStatus = d?.Status?.ToString(), CustomerId = d?.CustomerId, CustomerName = d?.CustomerName ?? s.CustomerName, Quantity = 1
            });
        }

        // ---- customers to contact ----
        var custIds = shipments.Select(s => s.CustomerId).Where(c => c != null).Distinct().ToList();
        var custs = await _context.Customer.AsNoTracking().Where(c => custIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.PhoneNumber, c.EmailAddress, c.Street, c.City, c.State, c.ZipCode, c.Country }).ToListAsync(ct);
        var customers = shipments
            .GroupBy(s => s.CustomerId ?? ("name:" + s.CustomerName))
            .Select(g =>
            {
                var c = custs.FirstOrDefault(x => x.Id == g.Key);
                var address = c == null ? null : string.Join(", ", new[] { c.Street, c.City, c.State, c.ZipCode, c.Country }.Where(x => !string.IsNullOrWhiteSpace(x)));
                return new RecallCustomerDto
                {
                    CustomerId = c?.Id, CustomerName = c?.Name ?? g.First().CustomerName, Phone = c?.PhoneNumber, Email = c?.EmailAddress, Address = address,
                    Quantity = g.Sum(x => x.Quantity), Deliveries = g.Select(x => x.DeliveryOrderId).Distinct().Count(), LastDelivery = g.Max(x => x.DeliveryDate)
                };
            })
            .OrderByDescending(c => c.Quantity).ToList();

        // ---- same product, delivered after the batch existed, with no batch recorded ----
        var unlinked = new List<RecallUnlinkedDto>();
        var productIds = batchRows.Select(b => b.ProductId).Distinct().ToList();
        if (productIds.Count > 0)
        {
            var since = batchRows.Min(b => b.CreatedAtUtc) ?? DateTime.UtcNow.AddYears(-1);
            var tx = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(x => x.ModuleName == nameof(Domain.Entities.DeliveryOrder) && x.Status == InventoryTransactionStatus.Confirmed &&
                            productIds.Contains(x.ProductId) && x.MovementDate >= since.Date)
                .Select(x => new { x.ModuleId, x.ProductId, Qty = Math.Abs(x.Movement ?? 0) }).ToListAsync(ct);
            var txIds = tx.Select(t => t.ModuleId).Distinct().ToList();
            var allPickedIds = await _context.DeliveryPickBatch.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(p => txIds.Contains(p.DeliveryOrderId)).Select(p => new { p.DeliveryOrderId, p.ProductId }).ToListAsync(ct);
            var dels = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false)
                .Where(d => txIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Number, d.DeliveryDate, Customer = d.SalesOrder!.Customer!.Name }).ToListAsync(ct);
            var names = batchRows.GroupBy(b => b.ProductId).ToDictionary(g => g.Key ?? "", g => g.First().ProductName);
            unlinked = tx.GroupBy(t => (t.ModuleId, t.ProductId))
                .Where(g => !allPickedIds.Any(p => p.DeliveryOrderId == g.Key.ModuleId && p.ProductId == g.Key.ProductId))
                .Select(g =>
                {
                    var d = dels.FirstOrDefault(x => x.Id == g.Key.ModuleId);
                    return new RecallUnlinkedDto
                    {
                        DeliveryOrderNumber = d?.Number, DeliveryDate = d?.DeliveryDate, CustomerName = d?.Customer,
                        ProductName = names.TryGetValue(g.Key.ProductId ?? "", out var n) ? n : null, Quantity = g.Sum(x => x.Qty)
                    };
                })
                .OrderByDescending(u => u.DeliveryDate).Take(200).ToList();
        }

        var batches = batchRows.Select(b => new RecallBatchDto
        {
            BatchId = b.Id, BatchNumber = b.BatchNumber, ProductId = b.ProductId, ProductNumber = b.ProductNumber, ProductName = b.ProductName,
            WarehouseName = whs.FirstOrDefault(w => w.Id == b.WarehouseId)?.Name, ExpiryDate = b.ExpiryDate, RegisteredAt = b.CreatedAtUtc,
            Remaining = b.Qty, Shipped = picks.Where(p => p.BatchId == b.Id).Sum(p => p.Qty)
        }).ToList();

        return new GetRecallTraceResult
        {
            Batches = batches,
            Shipments = shipments.OrderByDescending(s => s.DeliveryDate).ToList(),
            Customers = customers,
            Unlinked = unlinked,
            TotalShipped = shipments.Sum(s => s.Quantity),
            TotalRemaining = batches.Sum(b => b.Remaining)
        };
    }
}
