using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.BinMoveManager.Commands;

public class BinMoveResult
{
    public string? Message { get; set; }
}

// ---------------------------------------------------------------- move stock
public class MoveStockRequest : IRequest<BinMoveResult>
{
    public string? ProductId { get; init; }
    public string? FromLocationId { get; init; }
    public string? ToLocationId { get; init; }
    public double Quantity { get; init; }
    public string? Reason { get; init; }
    /// <summary>Move or Replenish.</summary>
    public string? MoveType { get; init; }
    public string? UpdatedById { get; init; }
}

public class MoveStockValidator : AbstractValidator<MoveStockRequest>
{
    public MoveStockValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.FromLocationId).NotEmpty().WithMessage("Choose the location to take stock from.");
        RuleFor(x => x.ToLocationId).NotEmpty().WithMessage("Choose the location to move stock into.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be above zero.");
    }
}

public class MoveStockHandler : IRequestHandler<MoveStockRequest, BinMoveResult>
{
    private readonly ICommandRepository<LocationStock> _stocks;
    private readonly ICommandRepository<StorageLocation> _locations;
    private readonly ICommandRepository<LocationMove> _moves;
    private readonly IUnitOfWork _unitOfWork;

    public MoveStockHandler(ICommandRepository<LocationStock> stocks, ICommandRepository<StorageLocation> locations, ICommandRepository<LocationMove> moves, IUnitOfWork unitOfWork)
    {
        _stocks = stocks; _locations = locations; _moves = moves; _unitOfWork = unitOfWork;
    }

    public async Task<BinMoveResult> Handle(MoveStockRequest request, CancellationToken ct)
    {
        if (request.FromLocationId == request.ToLocationId) throw new Exception("The two locations are the same.");
        var from = await _locations.GetAsync(request.FromLocationId ?? string.Empty, ct) ?? throw new Exception("Source location not found.");
        var to = await _locations.GetAsync(request.ToLocationId ?? string.Empty, ct) ?? throw new Exception("Destination location not found.");
        if (!to.IsActive) throw new Exception($"Location {to.Code} is inactive.");
        if (from.WarehouseId != to.WarehouseId) throw new Exception("Both locations must be in the same warehouse. Use a transfer to move stock between warehouses.");

        var source = _stocks.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(x => x.LocationId == from.Id && x.ProductId == request.ProductId);
        var have = source?.Quantity ?? 0;
        if (source == null || have + 1e-9 < request.Quantity) throw new Exception($"{from.Code} holds only {have:0.##} of this product.");

        var destRows = _stocks.GetQuery().ApplyIsDeletedFilter().Where(x => x.LocationId == to.Id).ToList();
        var used = destRows.Sum(x => x.Quantity ?? 0);
        if (to.Capacity.HasValue && used + request.Quantity > to.Capacity.Value + 1e-9)
            throw new Exception($"Location {to.Code} has room for {Math.Max(to.Capacity.Value - used, 0):0.##} more units.");

        source.Quantity = have - request.Quantity;
        source.UpdatedById = request.UpdatedById;
        _stocks.Update(source);

        var dest = destRows.FirstOrDefault(x => x.ProductId == request.ProductId);
        if (dest == null) await _stocks.CreateAsync(new LocationStock { CreatedById = request.UpdatedById, LocationId = to.Id, ProductId = request.ProductId, Quantity = request.Quantity }, ct);
        else { dest.Quantity = (dest.Quantity ?? 0) + request.Quantity; dest.UpdatedById = request.UpdatedById; _stocks.Update(dest); }

        await _moves.CreateAsync(new LocationMove
        {
            CreatedById = request.UpdatedById, ProductId = request.ProductId, FromLocationId = from.Id, ToLocationId = to.Id, Quantity = request.Quantity,
            MoveType = request.MoveType == "Replenish" ? "Replenish" : "Move", Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        }, ct);
        await _unitOfWork.SaveAsync(ct);
        return new BinMoveResult { Message = $"Moved {request.Quantity:0.##} from {from.Code} to {to.Code}." };
    }
}

// ---------------------------------------------------------------- replenishment rules
public class SaveReplenishmentRuleRequest : IRequest<BinMoveResult>
{
    public string? LocationId { get; init; }
    public string? ProductId { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public string? UpdatedById { get; init; }
}

public class SaveReplenishmentRuleValidator : AbstractValidator<SaveReplenishmentRuleRequest>
{
    public SaveReplenishmentRuleValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Min).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Max).GreaterThan(0).WithMessage("Max must be above zero.");
        RuleFor(x => x).Must(x => x.Max > x.Min).WithMessage("Max must be higher than Min.");
    }
}

public class SaveReplenishmentRuleHandler : IRequestHandler<SaveReplenishmentRuleRequest, BinMoveResult>
{
    private readonly ICommandRepository<ReplenishmentRule> _rules;
    private readonly IUnitOfWork _unitOfWork;

    public SaveReplenishmentRuleHandler(ICommandRepository<ReplenishmentRule> rules, IUnitOfWork unitOfWork)
    {
        _rules = rules; _unitOfWork = unitOfWork;
    }

    public async Task<BinMoveResult> Handle(SaveReplenishmentRuleRequest request, CancellationToken ct)
    {
        var row = _rules.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(x => x.LocationId == request.LocationId && x.ProductId == request.ProductId);
        if (row == null)
            await _rules.CreateAsync(new ReplenishmentRule { CreatedById = request.UpdatedById, LocationId = request.LocationId, ProductId = request.ProductId, MinQuantity = request.Min, MaxQuantity = request.Max }, ct);
        else { row.UpdatedById = request.UpdatedById; row.MinQuantity = request.Min; row.MaxQuantity = request.Max; _rules.Update(row); }
        await _unitOfWork.SaveAsync(ct);
        return new BinMoveResult { Message = "Rule saved." };
    }
}

public class DeleteReplenishmentRuleRequest : IRequest<BinMoveResult>
{
    public string? Id { get; init; }
    public string? UpdatedById { get; init; }
}

public class DeleteReplenishmentRuleHandler : IRequestHandler<DeleteReplenishmentRuleRequest, BinMoveResult>
{
    private readonly ICommandRepository<ReplenishmentRule> _rules;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteReplenishmentRuleHandler(ICommandRepository<ReplenishmentRule> rules, IUnitOfWork unitOfWork)
    {
        _rules = rules; _unitOfWork = unitOfWork;
    }

    public async Task<BinMoveResult> Handle(DeleteReplenishmentRuleRequest request, CancellationToken ct)
    {
        var row = await _rules.GetAsync(request.Id ?? string.Empty, ct) ?? throw new Exception("Rule not found.");
        row.UpdatedById = request.UpdatedById;
        _rules.Delete(row);
        await _unitOfWork.SaveAsync(ct);
        return new BinMoveResult { Message = "Rule removed." };
    }
}
