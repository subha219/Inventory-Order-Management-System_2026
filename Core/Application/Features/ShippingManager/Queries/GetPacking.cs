using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ShippingManager.Queries;

public class PackOrderDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public string? Customer { get; init; }
    public int Status { get; init; }
    public double Ordered { get; init; }
    public double Packed { get; init; }
    public int Shipments { get; init; }
    public int ShippedCount { get; init; }
}

public class PackLineDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Ordered { get; init; }
    public double Packed { get; init; }
}

public class PackItemDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Quantity { get; init; }
}

public class PackageDto
{
    public string? Id { get; init; }
    public int Number { get; init; }
    public string? PackageType { get; init; }
    public double? WeightKg { get; init; }
    public double? LengthCm { get; init; }
    public double? WidthCm { get; init; }
    public double? HeightCm { get; init; }
    public List<PackItemDto> Items { get; init; } = new();
}

public class ShipmentDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Status { get; init; }
    public string? Carrier { get; init; }
    public string? TrackingNumber { get; init; }
    public string? Notes { get; init; }
    public DateTime? ShippedAtUtc { get; init; }
    public double TotalWeightKg { get; init; }
    public List<PackageDto> Packages { get; init; } = new();
}

public class ShipToDto
{
    public string? Name { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? ZipCode { get; init; }
    public string? Country { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public class GetPackingResult
{
    public List<PackOrderDto> Orders { get; init; } = new();
    public string? OrderNumber { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public ShipToDto? ShipTo { get; init; }
    public List<PackLineDto> Lines { get; init; } = new();
    public List<ShipmentDto> Shipments { get; init; } = new();
}

public class GetPackingRequest : IRequest<GetPackingResult>
{
    public string? DeliveryOrderId { get; init; }
}

public class GetPackingHandler : IRequestHandler<GetPackingRequest, GetPackingResult>
{
    private readonly IQueryContext _context;

    public GetPackingHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetPackingResult> Handle(GetPackingRequest request, CancellationToken ct)
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
            .Select(x => new { x.ModuleId, x.ProductId, Qty = x.Movement ?? 0 }).ToListAsync(ct);
        var shipments = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => ids.Contains(s.DeliveryOrderId)).ToListAsync(ct);
        var shipIds = shipments.Select(s => s.Id).ToList();
        var items = await _context.ShipmentPackageItem.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(i => shipIds.Contains(i.ShipmentId)).ToListAsync(ct);

        var needBy = needRows.GroupBy(x => x.ModuleId).ToDictionary(g => g.Key ?? "", g => g.Sum(x => Math.Abs(x.Qty)));
        var packedByShipment = items.GroupBy(i => i.ShipmentId).ToDictionary(g => g.Key ?? "", g => g.Sum(i => i.Quantity ?? 0));

        var result = new GetPackingResult
        {
            Orders = orders.Select(o =>
            {
                var mine = shipments.Where(s => s.DeliveryOrderId == o.Id).ToList();
                return new PackOrderDto
                {
                    Id = o.Id, Number = o.Number, DeliveryDate = o.DeliveryDate, Customer = o.Customer, Status = o.Status,
                    Ordered = needBy.TryGetValue(o.Id, out var n) ? n : 0,
                    Packed = mine.Sum(s => packedByShipment.TryGetValue(s.Id, out var p) ? p : 0),
                    Shipments = mine.Count, ShippedCount = mine.Count(s => s.Status == "Shipped")
                };
            }).ToList()
        };

        var id = request.DeliveryOrderId;
        if (string.IsNullOrEmpty(id) || !ids.Contains(id)) return result;

        var order = orders.First(o => o.Id == id);
        var cust = await _context.DeliveryOrder.AsNoTracking().Where(d => d.Id == id)
            .Select(c => c.SalesOrder!.Customer).FirstOrDefaultAsync(ct);
        var productIds = needRows.Where(x => x.ModuleId == id).Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Product.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);

        var mineShip = shipments.Where(s => s.DeliveryOrderId == id).OrderBy(s => s.CreatedAtUtc).ToList();
        var mineIds = mineShip.Select(s => s.Id).ToList();
        var packages = await _context.ShipmentPackage.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(p => mineIds.Contains(p.ShipmentId)).ToListAsync(ct);
        var mineItems = items.Where(i => mineIds.Contains(i.ShipmentId)).ToList();

        PackItemDto ItemDto(Domain.Entities.ShipmentPackageItem i)
        {
            var p = products.FirstOrDefault(x => x.Id == i.ProductId);
            return new PackItemDto { ProductId = i.ProductId, ProductNumber = p?.Number, ProductName = p?.Name, Quantity = i.Quantity ?? 0 };
        }

        var lines = needRows.Where(x => x.ModuleId == id).GroupBy(x => x.ProductId).Select(g =>
        {
            var p = products.FirstOrDefault(x => x.Id == g.Key);
            return new PackLineDto
            {
                ProductId = g.Key, ProductNumber = p?.Number, ProductName = p?.Name, Ordered = g.Sum(x => Math.Abs(x.Qty)),
                Packed = mineItems.Where(i => i.ProductId == g.Key).Sum(i => i.Quantity ?? 0)
            };
        }).ToList();

        return new GetPackingResult
        {
            Orders = result.Orders, OrderNumber = order.Number, DeliveryDate = order.DeliveryDate, Lines = lines,
            ShipTo = cust == null ? null : new ShipToDto { Name = cust.Name, Street = cust.Street, City = cust.City, State = cust.State, ZipCode = cust.ZipCode, Country = cust.Country, Phone = cust.PhoneNumber, Email = cust.EmailAddress },
            Shipments = mineShip.Select(s => new ShipmentDto
            {
                Id = s.Id, Number = s.Number, Status = s.Status, Carrier = s.Carrier, TrackingNumber = s.TrackingNumber, Notes = s.Notes, ShippedAtUtc = s.ShippedAtUtc,
                TotalWeightKg = packages.Where(p => p.ShipmentId == s.Id).Sum(p => p.WeightKg ?? 0),
                Packages = packages.Where(p => p.ShipmentId == s.Id).OrderBy(p => p.PackageNumber).Select(p => new PackageDto
                {
                    Id = p.Id, Number = p.PackageNumber ?? 0, PackageType = p.PackageType, WeightKg = p.WeightKg, LengthCm = p.LengthCm, WidthCm = p.WidthCm, HeightCm = p.HeightCm,
                    Items = mineItems.Where(i => i.ShipmentPackageId == p.Id).Select(ItemDto).ToList()
                }).ToList()
            }).ToList()
        };
    }
}
