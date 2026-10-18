using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.InventoryTransactionManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.StockHoldManager.Commands;

public class HoldResult
{
    public string? Id { get; set; }
    public string? Message { get; set; }
}

public class PlaceHoldRequest : IRequest<HoldResult>
{
    public string? ProductId { get; init; }
    public string? WarehouseId { get; init; }
    public double Quantity { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public string? UserId { get; init; }
}

public class PlaceHoldValidator : AbstractValidator<PlaceHoldRequest>
{
    public PlaceHoldValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Choose the product.");
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Choose the warehouse.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Enter how many units to hold.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class PlaceHoldHandler : IRequestHandler<PlaceHoldRequest, HoldResult>
{
    private readonly ICommandRepository<StockHold> _holds;
    private readonly InventoryTransactionService _inventory;
    private readonly IUnitOfWork _unitOfWork;

    public PlaceHoldHandler(ICommandRepository<StockHold> holds, InventoryTransactionService inventory, IUnitOfWork unitOfWork)
    {
        _holds = holds; _inventory = inventory; _unitOfWork = unitOfWork;
    }

    public async Task<HoldResult> Handle(PlaceHoldRequest r, CancellationToken ct)
    {
        var onHand = _inventory.GetStock(r.WarehouseId, r.ProductId);
        var wh = r.WarehouseId; var pr = r.ProductId;
        var held = _holds.GetQuery().ApplyIsDeletedFilter().Where(h => h.Status == "Active" && h.WarehouseId == wh && h.ProductId == pr).Sum(h => h.Quantity ?? 0);
        var free = onHand - held;
        if (r.Quantity > free + 1e-9) throw new Exception($"Only {free:0.##} units are free to hold here ({onHand:0.##} on hand, {held:0.##} already on hold).");

        var h = new StockHold
        {
            ProductId = r.ProductId, WarehouseId = r.WarehouseId, Quantity = r.Quantity, OriginalQuantity = r.Quantity, Reason = r.Reason,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(), Status = "Active", CreatedById = r.UserId
        };
        await _holds.CreateAsync(h, ct);
        await _unitOfWork.SaveAsync(ct);
        return new HoldResult { Id = h.Id, Message = $"{r.Quantity:0.##} units are on hold." };
    }
}

public class ReleaseHoldRequest : IRequest<HoldResult>
{
    public string? Id { get; init; }
    /// <summary>Units to release; leave empty to release everything.</summary>
    public double? Quantity { get; init; }
    public string? UserId { get; init; }
}

public class ReleaseHoldHandler : IRequestHandler<ReleaseHoldRequest, HoldResult>
{
    private readonly ICommandRepository<StockHold> _holds;
    private readonly IUnitOfWork _unitOfWork;

    public ReleaseHoldHandler(ICommandRepository<StockHold> holds, IUnitOfWork unitOfWork)
    {
        _holds = holds; _unitOfWork = unitOfWork;
    }

    public async Task<HoldResult> Handle(ReleaseHoldRequest r, CancellationToken ct)
    {
        var h = await _holds.GetAsync(r.Id ?? string.Empty, ct) ?? throw new Exception("Hold not found.");
        if (h.Status != "Active") throw new Exception("This hold is already released.");
        var qty = r.Quantity ?? h.Quantity ?? 0;
        if (qty <= 0 || qty > (h.Quantity ?? 0) + 1e-9) throw new Exception("Enter a quantity between 1 and the units still on hold.");
        h.Quantity = Math.Max((h.Quantity ?? 0) - qty, 0);
        if (h.Quantity <= 1e-9) { h.Quantity = 0; h.Status = "Released"; h.ReleasedAtUtc = DateTime.UtcNow; h.ReleasedById = r.UserId; }
        h.UpdatedById = r.UserId;
        _holds.Update(h);
        await _unitOfWork.SaveAsync(ct);
        return new HoldResult { Id = h.Id, Message = $"{qty:0.##} units released." };
    }
}
