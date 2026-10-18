using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.ProductSerialManager.Commands;

public class SerialResult
{
    public int Count { get; set; }
    public string? Id { get; set; }
}

// ---------------------------------------------------------------- register
public class RegisterSerialsRequest : IRequest<SerialResult>
{
    public string? ProductId { get; init; }
    public string? WarehouseId { get; init; }
    public string? LocationId { get; init; }
    public string? GoodsReceiveId { get; init; }
    public List<string>? Serials { get; init; }
    public string? Notes { get; init; }
    public string? UpdatedById { get; init; }
}

public class RegisterSerialsValidator : AbstractValidator<RegisterSerialsRequest>
{
    public RegisterSerialsValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Serials).NotEmpty().WithMessage("Enter at least one serial number.");
    }
}

public class RegisterSerialsHandler : IRequestHandler<RegisterSerialsRequest, SerialResult>
{
    private readonly ICommandRepository<ProductSerial> _serials;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly ICommandRepository<GoodsReceive> _receipts;
    private readonly ICommandRepository<StorageLocation> _locations;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterSerialsHandler(ICommandRepository<ProductSerial> serials, ICommandRepository<InventoryTransaction> transactions,
        ICommandRepository<GoodsReceive> receipts, ICommandRepository<StorageLocation> locations, IUnitOfWork unitOfWork)
    {
        _serials = serials;
        _transactions = transactions;
        _receipts = receipts;
        _locations = locations;
        _unitOfWork = unitOfWork;
    }

    public async Task<SerialResult> Handle(RegisterSerialsRequest request, CancellationToken ct)
    {
        var list = (request.Serials ?? new List<string>()).Select(s => (s ?? string.Empty).Trim()).Where(s => s.Length > 0).ToList();
        if (list.Count == 0) throw new Exception("Enter at least one serial number.");
        if (list.Count > 2000) throw new Exception("Register at most 2000 serial numbers at a time.");
        if (list.Any(s => s.Length > 200)) throw new Exception("A serial number is longer than 200 characters.");
        var dupInInput = list.GroupBy(s => s, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (dupInInput != null) throw new Exception($"Serial number {dupInInput.Key} appears more than once in your list.");

        var existing = _serials.GetQuery().ApplyIsDeletedFilter().Where(x => x.ProductId == request.ProductId)
            .Select(x => new { x.SerialNumber, x.Status, x.WarehouseId }).ToList();
        var taken = list.FirstOrDefault(s => existing.Any(e => string.Equals(e.SerialNumber, s, StringComparison.OrdinalIgnoreCase)));
        if (taken != null) throw new Exception($"Serial number {taken} is already registered for this product.");

        // serials in stock cannot outnumber the units the warehouse actually holds
        var onHand = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.WarehouseId == request.WarehouseId && x.ProductId == request.ProductId && x.Status == InventoryTransactionStatus.Confirmed)
            .Sum(x => x.Stock ?? 0);
        var inStock = existing.Count(e => (e.Status == "InStock" || e.Status == "Returned") && e.WarehouseId == request.WarehouseId);
        if (inStock + list.Count > onHand + 1e-9)
            throw new Exception($"The warehouse holds {onHand:0.##} units and {inStock} already have serial numbers, so only {Math.Max(onHand - inStock, 0):0.##} more can be registered.");

        string? receiptNumber = null;
        if (!string.IsNullOrEmpty(request.GoodsReceiveId))
            receiptNumber = (await _receipts.GetAsync(request.GoodsReceiveId, ct))?.Number;
        if (!string.IsNullOrEmpty(request.LocationId))
        {
            var loc = await _locations.GetAsync(request.LocationId, ct) ?? throw new Exception("Location not found.");
            if (loc.WarehouseId != request.WarehouseId) throw new Exception($"Location {loc.Code} is in a different warehouse.");
        }

        foreach (var s in list)
        {
            await _serials.CreateAsync(new ProductSerial
            {
                CreatedById = request.UpdatedById, ProductId = request.ProductId, SerialNumber = s, Status = "InStock",
                WarehouseId = request.WarehouseId, LocationId = string.IsNullOrEmpty(request.LocationId) ? null : request.LocationId,
                GoodsReceiveId = string.IsNullOrEmpty(request.GoodsReceiveId) ? null : request.GoodsReceiveId,
                GoodsReceiveNumber = receiptNumber, Notes = request.Notes
            }, ct);
        }
        await _unitOfWork.SaveAsync(ct);
        return new SerialResult { Count = list.Count };
    }
}

// ---------------------------------------------------------------- change status
public class ChangeSerialStatusRequest : IRequest<SerialResult>
{
    public string? Id { get; init; }
    /// <summary>InStock, Shipped, Returned or Scrapped.</summary>
    public string? Status { get; init; }
    /// <summary>Required when shipping: the delivery order the unit left on.</summary>
    public string? DeliveryOrderId { get; init; }
    public string? Notes { get; init; }
    public string? UpdatedById { get; init; }
}

public class ChangeSerialStatusValidator : AbstractValidator<ChangeSerialStatusRequest>
{
    public ChangeSerialStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).Must(s => s == "InStock" || s == "Shipped" || s == "Returned" || s == "Scrapped").WithMessage("Unknown status.");
    }
}

public class ChangeSerialStatusHandler : IRequestHandler<ChangeSerialStatusRequest, SerialResult>
{
    private readonly ICommandRepository<ProductSerial> _serials;
    private readonly ICommandRepository<DeliveryOrder> _deliveries;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly ICommandRepository<SalesOrder> _salesOrders;
    private readonly ICommandRepository<Customer> _customers;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeSerialStatusHandler(ICommandRepository<ProductSerial> serials, ICommandRepository<DeliveryOrder> deliveries,
        ICommandRepository<InventoryTransaction> transactions, ICommandRepository<SalesOrder> salesOrders,
        ICommandRepository<Customer> customers, IUnitOfWork unitOfWork)
    {
        _serials = serials;
        _deliveries = deliveries;
        _transactions = transactions;
        _salesOrders = salesOrders;
        _customers = customers;
        _unitOfWork = unitOfWork;
    }

    public async Task<SerialResult> Handle(ChangeSerialStatusRequest request, CancellationToken ct)
    {
        var s = await _serials.GetAsync(request.Id ?? string.Empty, ct) ?? throw new Exception("Serial number not found.");
        s.UpdatedById = request.UpdatedById;

        if (request.Status == "Shipped")
        {
            if (s.Status == "Shipped") throw new Exception($"{s.SerialNumber} is already shipped.");
            if (s.Status == "Scrapped") throw new Exception($"{s.SerialNumber} was scrapped and cannot be shipped.");
            if (string.IsNullOrEmpty(request.DeliveryOrderId)) throw new Exception("Choose the delivery order this unit left on.");
            var delivery = await _deliveries.GetAsync(request.DeliveryOrderId, ct) ?? throw new Exception("Delivery order not found.");
            var hasProduct = _transactions.GetQuery().ApplyIsDeletedFilter()
                .Any(x => x.ModuleId == delivery.Id && x.ModuleName == nameof(DeliveryOrder) && x.ProductId == s.ProductId);
            if (!hasProduct) throw new Exception($"{delivery.Number} does not include this product.");

            // a delivery cannot ship more serials than it carries units
            var carried = _transactions.GetQuery().ApplyIsDeletedFilter()
                .Where(x => x.ModuleId == delivery.Id && x.ModuleName == nameof(DeliveryOrder) && x.ProductId == s.ProductId)
                .Sum(x => Math.Abs(x.Movement ?? 0));
            var already = _serials.GetQuery().ApplyIsDeletedFilter().Count(x => x.DeliveryOrderId == delivery.Id && x.ProductId == s.ProductId && x.Status == "Shipped");
            if (already + 1 > carried + 1e-9) throw new Exception($"{delivery.Number} carries {carried:0.##} units of this product and all have serial numbers.");

            string? customer = null;
            var so = delivery.SalesOrderId == null ? null : await _salesOrders.GetAsync(delivery.SalesOrderId, ct);
            if (so?.CustomerId != null) customer = (await _customers.GetAsync(so.CustomerId, ct))?.Name;

            s.Status = "Shipped";
            s.DeliveryOrderId = delivery.Id;
            s.DeliveryOrderNumber = delivery.Number;
            s.CustomerName = customer;
            s.ShippedAtUtc = DateTime.UtcNow;
            s.LocationId = null;
        }
        else
        {
            s.Status = request.Status;
            if (request.Status != "Shipped" && s.DeliveryOrderId != null && request.Status != "Scrapped")
            {
                // coming back keeps the old delivery in the note so the history is not lost
                s.Notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? $"Was shipped on {s.DeliveryOrderNumber} to {s.CustomerName}"
                    : request.Notes;
                s.DeliveryOrderId = null; s.DeliveryOrderNumber = null; s.CustomerName = null; s.ShippedAtUtc = null;
            }
            else if (!string.IsNullOrWhiteSpace(request.Notes)) s.Notes = request.Notes;
        }

        _serials.Update(s);
        await _unitOfWork.SaveAsync(ct);
        return new SerialResult { Id = s.Id, Count = 1 };
    }
}

// ---------------------------------------------------------------- delete (typing mistakes)
public class DeleteSerialRequest : IRequest<SerialResult>
{
    public string? Id { get; init; }
    public string? UpdatedById { get; init; }
}

public class DeleteSerialHandler : IRequestHandler<DeleteSerialRequest, SerialResult>
{
    private readonly ICommandRepository<ProductSerial> _serials;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSerialHandler(ICommandRepository<ProductSerial> serials, IUnitOfWork unitOfWork)
    {
        _serials = serials;
        _unitOfWork = unitOfWork;
    }

    public async Task<SerialResult> Handle(DeleteSerialRequest request, CancellationToken ct)
    {
        var s = await _serials.GetAsync(request.Id ?? string.Empty, ct) ?? throw new Exception("Serial number not found.");
        if (s.Status == "Shipped") throw new Exception("A shipped serial number cannot be deleted. Mark it Returned first if it came back.");
        s.UpdatedById = request.UpdatedById;
        _serials.Delete(s);
        await _unitOfWork.SaveAsync(ct);
        return new SerialResult { Id = s.Id, Count = 1 };
    }
}
