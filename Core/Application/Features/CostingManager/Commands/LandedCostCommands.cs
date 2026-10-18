using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.CostingManager.Commands;

public class LandedCostResult
{
    public string? Id { get; set; }
}

public class SaveLandedCostRequest : IRequest<LandedCostResult>
{
    public string? GoodsReceiveId { get; init; }
    public string? Description { get; init; }
    public double Amount { get; init; }
    /// <summary>Value or Quantity.</summary>
    public string? Basis { get; init; }
    public string? CreatedById { get; init; }
}

public class SaveLandedCostValidator : AbstractValidator<SaveLandedCostRequest>
{
    public SaveLandedCostValidator()
    {
        RuleFor(x => x.GoodsReceiveId).NotEmpty().WithMessage("Choose a goods receipt.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Describe the cost, for example Freight.").MaximumLength(255);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be above zero.");
    }
}

public class SaveLandedCostHandler : IRequestHandler<SaveLandedCostRequest, LandedCostResult>
{
    private readonly ICommandRepository<LandedCost> _costs;
    private readonly ICommandRepository<GoodsReceive> _receipts;
    private readonly IUnitOfWork _unitOfWork;

    public SaveLandedCostHandler(ICommandRepository<LandedCost> costs, ICommandRepository<GoodsReceive> receipts, IUnitOfWork unitOfWork)
    {
        _costs = costs; _receipts = receipts; _unitOfWork = unitOfWork;
    }

    public async Task<LandedCostResult> Handle(SaveLandedCostRequest request, CancellationToken ct)
    {
        var receipt = await _receipts.GetAsync(request.GoodsReceiveId ?? string.Empty, ct) ?? throw new Exception("Goods receipt not found.");
        if (receipt.Status != GoodsReceiveStatus.Confirmed) throw new Exception($"{receipt.Number} is not confirmed, so its goods have not arrived yet.");
        var c = new LandedCost
        {
            CreatedById = request.CreatedById, GoodsReceiveId = receipt.Id, Description = request.Description!.Trim(), Amount = request.Amount,
            Basis = request.Basis == "Quantity" ? "Quantity" : "Value"
        };
        await _costs.CreateAsync(c, ct);
        await _unitOfWork.SaveAsync(ct);
        return new LandedCostResult { Id = c.Id };
    }
}

public class DeleteLandedCostRequest : IRequest<LandedCostResult>
{
    public string? Id { get; init; }
    public string? UpdatedById { get; init; }
}

public class DeleteLandedCostHandler : IRequestHandler<DeleteLandedCostRequest, LandedCostResult>
{
    private readonly ICommandRepository<LandedCost> _costs;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteLandedCostHandler(ICommandRepository<LandedCost> costs, IUnitOfWork unitOfWork)
    {
        _costs = costs; _unitOfWork = unitOfWork;
    }

    public async Task<LandedCostResult> Handle(DeleteLandedCostRequest request, CancellationToken ct)
    {
        var c = await _costs.GetAsync(request.Id ?? string.Empty, ct) ?? throw new Exception("Cost not found.");
        c.UpdatedById = request.UpdatedById;
        _costs.Delete(c);
        await _unitOfWork.SaveAsync(ct);
        return new LandedCostResult { Id = c.Id };
    }
}
