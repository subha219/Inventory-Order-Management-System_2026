using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.ShippingManager.Commands;

public class ShippingResult
{
    public string? Id { get; set; }
    public string? Number { get; set; }
    public string? Note { get; set; }
}

// ---------------------------------------------------------------- create
public class CreateShipmentRequest : IRequest<ShippingResult>
{
    public string? DeliveryOrderId { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateShipmentHandler : IRequestHandler<CreateShipmentRequest, ShippingResult>
{
    private readonly ICommandRepository<Shipment> _shipments;
    private readonly ICommandRepository<DeliveryOrder> _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;

    public CreateShipmentHandler(ICommandRepository<Shipment> shipments, ICommandRepository<DeliveryOrder> orders, IUnitOfWork unitOfWork, NumberSequenceService numbers)
    {
        _shipments = shipments; _orders = orders; _unitOfWork = unitOfWork; _numbers = numbers;
    }

    public async Task<ShippingResult> Handle(CreateShipmentRequest request, CancellationToken ct)
    {
        var order = await _orders.GetAsync(request.DeliveryOrderId ?? string.Empty, ct) ?? throw new Exception("Delivery order not found.");
        if (order.Status != DeliveryOrderStatus.Draft && order.Status != DeliveryOrderStatus.Confirmed)
            throw new Exception($"{order.Number} is {order.Status}, so it cannot be shipped.");
        var s = new Shipment
        {
            CreatedById = request.CreatedById, DeliveryOrderId = order.Id, Status = "Packing", PublicToken = Guid.NewGuid().ToString("N"),
            Number = _numbers.GenerateNumber(nameof(Shipment), "", "SHP")
        };
        await _shipments.CreateAsync(s, ct);
        await _unitOfWork.SaveAsync(ct);
        return new ShippingResult { Id = s.Id, Number = s.Number };
    }
}

// ---------------------------------------------------------------- packages
public class PackItemInput
{
    public string? ProductId { get; init; }
    public double Quantity { get; init; }
}

public class PackageInput
{
    public string? PackageType { get; init; }
    public double? WeightKg { get; init; }
    public double? LengthCm { get; init; }
    public double? WidthCm { get; init; }
    public double? HeightCm { get; init; }
    public List<PackItemInput>? Items { get; init; }
}

public class SavePackagesRequest : IRequest<ShippingResult>
{
    public string? ShipmentId { get; init; }
    public List<PackageInput>? Packages { get; init; }
    public string? UpdatedById { get; init; }
}

public class SavePackagesValidator : AbstractValidator<SavePackagesRequest>
{
    public SavePackagesValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
    }
}

public class SavePackagesHandler : IRequestHandler<SavePackagesRequest, ShippingResult>
{
    private readonly ICommandRepository<Shipment> _shipments;
    private readonly ICommandRepository<ShipmentPackage> _packages;
    private readonly ICommandRepository<ShipmentPackageItem> _items;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly IUnitOfWork _unitOfWork;

    public SavePackagesHandler(ICommandRepository<Shipment> shipments, ICommandRepository<ShipmentPackage> packages,
        ICommandRepository<ShipmentPackageItem> items, ICommandRepository<InventoryTransaction> transactions, IUnitOfWork unitOfWork)
    {
        _shipments = shipments; _packages = packages; _items = items; _transactions = transactions; _unitOfWork = unitOfWork;
    }

    public async Task<ShippingResult> Handle(SavePackagesRequest request, CancellationToken ct)
    {
        var shipment = await _shipments.GetAsync(request.ShipmentId ?? string.Empty, ct) ?? throw new Exception("Shipment not found.");
        if (shipment.Status == "Shipped") throw new Exception($"{shipment.Number} has already shipped and cannot be changed.");

        var packages = (request.Packages ?? new List<PackageInput>())
            .Where(p => (p.Items ?? new List<PackItemInput>()).Any(i => i.Quantity > 0)).ToList();

        // what the order asks for, and what other shipments of the same order already hold
        var ordered = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ModuleId == shipment.DeliveryOrderId && x.ModuleName == nameof(DeliveryOrder))
            .Select(x => new { x.ProductId, Qty = x.Movement ?? 0 }).ToList()
            .GroupBy(x => x.ProductId ?? "").ToDictionary(g => g.Key, g => g.Sum(x => Math.Abs(x.Qty)));
        var otherShipmentIds = _shipments.GetQuery().ApplyIsDeletedFilter()
            .Where(s => s.DeliveryOrderId == shipment.DeliveryOrderId && s.Id != shipment.Id).Select(s => s.Id).ToList();
        var others = _items.GetQuery().ApplyIsDeletedFilter().Where(i => otherShipmentIds.Contains(i.ShipmentId!))
            .Select(i => new { i.ProductId, Qty = i.Quantity ?? 0 }).ToList()
            .GroupBy(x => x.ProductId ?? "").ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        var mine = packages.SelectMany(p => p.Items!).Where(i => i.Quantity > 0).GroupBy(i => i.ProductId ?? "").ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        foreach (var (pid, qty) in mine)
        {
            if (!ordered.TryGetValue(pid, out var max)) throw new Exception("A product is not on this delivery order.");
            var already = others.TryGetValue(pid, out var o) ? o : 0;
            if (qty + already > max + 1e-9) throw new Exception($"You are packing {qty:0.##} of a product but only {Math.Max(max - already, 0):0.##} are left to pack on this order.");
        }
        foreach (var p in packages)
        {
            if ((p.WeightKg ?? 0) < 0 || (p.LengthCm ?? 0) < 0 || (p.WidthCm ?? 0) < 0 || (p.HeightCm ?? 0) < 0) throw new Exception("Weight and size cannot be negative.");
        }

        // replace the packing of this shipment
        foreach (var old in _items.GetQuery().ApplyIsDeletedFilter().Where(i => i.ShipmentId == shipment.Id).ToList()) _items.Delete(old);
        foreach (var old in _packages.GetQuery().ApplyIsDeletedFilter().Where(p => p.ShipmentId == shipment.Id).ToList()) _packages.Delete(old);

        var n = 0;
        foreach (var p in packages)
        {
            n++;
            var pack = new ShipmentPackage
            {
                CreatedById = request.UpdatedById, ShipmentId = shipment.Id, PackageNumber = n, PackageType = string.IsNullOrWhiteSpace(p.PackageType) ? "Carton" : p.PackageType.Trim(),
                WeightKg = p.WeightKg, LengthCm = p.LengthCm, WidthCm = p.WidthCm, HeightCm = p.HeightCm
            };
            await _packages.CreateAsync(pack, ct);
            foreach (var i in p.Items!.Where(i => i.Quantity > 0))
                await _items.CreateAsync(new ShipmentPackageItem { CreatedById = request.UpdatedById, ShipmentId = shipment.Id, ShipmentPackageId = pack.Id, ProductId = i.ProductId, Quantity = i.Quantity }, ct);
        }
        shipment.UpdatedById = request.UpdatedById;
        _shipments.Update(shipment);
        await _unitOfWork.SaveAsync(ct);
        return new ShippingResult { Id = shipment.Id, Number = shipment.Number };
    }
}

// ---------------------------------------------------------------- ship
public class ShipShipmentRequest : IRequest<ShippingResult>
{
    public string? ShipmentId { get; init; }
    public string? Carrier { get; init; }
    public string? TrackingNumber { get; init; }
    public string? Notes { get; init; }
    public string? UpdatedById { get; init; }
}

public class ShipShipmentValidator : AbstractValidator<ShipShipmentRequest>
{
    public ShipShipmentValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
        RuleFor(x => x.Carrier).NotEmpty().WithMessage("Enter the carrier.");
    }
}

public class ShipShipmentHandler : IRequestHandler<ShipShipmentRequest, ShippingResult>
{
    private readonly ICommandRepository<Shipment> _shipments;
    private readonly ICommandRepository<ShipmentPackage> _packages;
    private readonly ICommandRepository<DeliveryOrder> _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Application.Features.IntegrationManager.WebhookDispatcher _webhooks;

    public ShipShipmentHandler(ICommandRepository<Shipment> shipments, ICommandRepository<ShipmentPackage> packages, ICommandRepository<DeliveryOrder> orders, IUnitOfWork unitOfWork, Application.Features.IntegrationManager.WebhookDispatcher webhooks)
    {
        _shipments = shipments; _packages = packages; _orders = orders; _unitOfWork = unitOfWork; _webhooks = webhooks;
    }

    public async Task<ShippingResult> Handle(ShipShipmentRequest request, CancellationToken ct)
    {
        var s = await _shipments.GetAsync(request.ShipmentId ?? string.Empty, ct) ?? throw new Exception("Shipment not found.");
        if (s.Status == "Shipped") throw new Exception($"{s.Number} has already shipped.");
        if (!_packages.GetQuery().ApplyIsDeletedFilter().Any(p => p.ShipmentId == s.Id)) throw new Exception("Pack at least one carton before shipping.");

        s.Status = "Shipped";
        s.Carrier = request.Carrier?.Trim();
        s.TrackingNumber = string.IsNullOrWhiteSpace(request.TrackingNumber) ? null : request.TrackingNumber.Trim();
        s.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        s.ShippedAtUtc = DateTime.UtcNow;
        s.ShippedById = request.UpdatedById;
        s.UpdatedById = request.UpdatedById;
        if (string.IsNullOrEmpty(s.PublicToken)) s.PublicToken = Guid.NewGuid().ToString("N");
        _shipments.Update(s);
        await _unitOfWork.SaveAsync(ct);

        var order = await _orders.GetAsync(s.DeliveryOrderId ?? string.Empty, ct);
        await _webhooks.DispatchAsync(Application.Features.IntegrationManager.WebhookEvents.ShipmentShipped, new
        {
            shipmentNumber = s.Number, orderNumber = order?.Number, carrier = s.Carrier, trackingNumber = s.TrackingNumber,
            shippedAtUtc = s.ShippedAtUtc, trackingToken = s.PublicToken
        }, ct);
        var note = order != null && order.Status == DeliveryOrderStatus.Draft
            ? $"{order.Number} is still a Draft. Confirm it on the Delivery Order page so the stock leaves the warehouse."
            : null;
        return new ShippingResult { Id = s.Id, Number = s.Number, Note = note };
    }
}

// ---------------------------------------------------------------- delete
public class DeleteShipmentRequest : IRequest<ShippingResult>
{
    public string? ShipmentId { get; init; }
    public string? UpdatedById { get; init; }
}

public class DeleteShipmentHandler : IRequestHandler<DeleteShipmentRequest, ShippingResult>
{
    private readonly ICommandRepository<Shipment> _shipments;
    private readonly ICommandRepository<ShipmentPackage> _packages;
    private readonly ICommandRepository<ShipmentPackageItem> _items;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteShipmentHandler(ICommandRepository<Shipment> shipments, ICommandRepository<ShipmentPackage> packages, ICommandRepository<ShipmentPackageItem> items, IUnitOfWork unitOfWork)
    {
        _shipments = shipments; _packages = packages; _items = items; _unitOfWork = unitOfWork;
    }

    public async Task<ShippingResult> Handle(DeleteShipmentRequest request, CancellationToken ct)
    {
        var s = await _shipments.GetAsync(request.ShipmentId ?? string.Empty, ct) ?? throw new Exception("Shipment not found.");
        if (s.Status == "Shipped") throw new Exception($"{s.Number} has already shipped and cannot be deleted.");
        foreach (var i in _items.GetQuery().ApplyIsDeletedFilter().Where(i => i.ShipmentId == s.Id).ToList()) _items.Delete(i);
        foreach (var p in _packages.GetQuery().ApplyIsDeletedFilter().Where(p => p.ShipmentId == s.Id).ToList()) _packages.Delete(p);
        s.UpdatedById = request.UpdatedById;
        _shipments.Delete(s);
        await _unitOfWork.SaveAsync(ct);
        return new ShippingResult { Id = s.Id, Number = s.Number };
    }
}
