using Application.Common.Repositories;
using Application.Features.InventoryTransactionManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.ApprovalManager.Commands;

public class DecideApprovalResult
{
    public string? Id { get; set; }
    public string? Status { get; set; }
}

public class DecideApprovalRequest : IRequest<DecideApprovalResult>
{
    public string? Id { get; init; }
    /// <summary>True to approve and confirm the document, false to reject.</summary>
    public bool Approve { get; init; }
    public string? Note { get; init; }
    public string? DecidedById { get; init; }
}

public class DecideApprovalValidator : AbstractValidator<DecideApprovalRequest>
{
    public DecideApprovalValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DecideApprovalHandler : IRequestHandler<DecideApprovalRequest, DecideApprovalResult>
{
    private readonly ICommandRepository<ApprovalRequest> _requests;
    private readonly ICommandRepository<PositiveAdjustment> _positive;
    private readonly ICommandRepository<NegativeAdjustment> _negative;
    private readonly ICommandRepository<Scrapping> _scrapping;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InventoryTransactionService _inventoryTransactionService;

    public DecideApprovalHandler(
        ICommandRepository<ApprovalRequest> requests,
        ICommandRepository<PositiveAdjustment> positive,
        ICommandRepository<NegativeAdjustment> negative,
        ICommandRepository<Scrapping> scrapping,
        IUnitOfWork unitOfWork,
        InventoryTransactionService inventoryTransactionService)
    {
        _requests = requests;
        _positive = positive;
        _negative = negative;
        _scrapping = scrapping;
        _unitOfWork = unitOfWork;
        _inventoryTransactionService = inventoryTransactionService;
    }

    public async Task<DecideApprovalResult> Handle(DecideApprovalRequest request, CancellationToken ct)
    {
        var approval = await _requests.GetAsync(request.Id ?? string.Empty, ct);
        if (approval == null) throw new Exception("Approval request not found.");
        if (approval.Status != "Pending") throw new Exception($"This request was already {approval.Status?.ToLowerInvariant()}.");

        if (request.Approve)
        {
            var id = approval.EntityId ?? string.Empty;
            switch (approval.EntityType)
            {
                case nameof(PositiveAdjustment):
                    var p = await _positive.GetAsync(id, ct) ?? throw new Exception("The adjustment no longer exists.");
                    p.UpdatedById = request.DecidedById;
                    p.Status = AdjustmentStatus.Confirmed;
                    _positive.Update(p);
                    await _unitOfWork.SaveAsync(ct);
                    await _inventoryTransactionService.PropagateParentUpdate(p.Id, nameof(PositiveAdjustment), p.AdjustmentDate,
                        (InventoryTransactionStatus?)p.Status, p.IsDeleted, p.UpdatedById, null, ct);
                    break;
                case nameof(NegativeAdjustment):
                    var n = await _negative.GetAsync(id, ct) ?? throw new Exception("The adjustment no longer exists.");
                    n.UpdatedById = request.DecidedById;
                    n.Status = AdjustmentStatus.Confirmed;
                    _negative.Update(n);
                    await _unitOfWork.SaveAsync(ct);
                    await _inventoryTransactionService.PropagateParentUpdate(n.Id, nameof(NegativeAdjustment), n.AdjustmentDate,
                        (InventoryTransactionStatus?)n.Status, n.IsDeleted, n.UpdatedById, null, ct);
                    break;
                case nameof(Scrapping):
                    var s = await _scrapping.GetAsync(id, ct) ?? throw new Exception("The scrapping document no longer exists.");
                    s.UpdatedById = request.DecidedById;
                    s.Status = ScrappingStatus.Confirmed;
                    _scrapping.Update(s);
                    await _unitOfWork.SaveAsync(ct);
                    await _inventoryTransactionService.PropagateParentUpdate(s.Id, nameof(Scrapping), s.ScrappingDate,
                        (InventoryTransactionStatus?)s.Status, s.IsDeleted, s.UpdatedById, s.WarehouseId, ct);
                    break;
                default:
                    throw new Exception("Unknown document type.");
            }
        }

        approval.UpdatedById = request.DecidedById;
        approval.Status = request.Approve ? "Approved" : "Rejected";
        approval.DecidedById = request.DecidedById;
        approval.DecidedAtUtc = DateTime.UtcNow;
        approval.DecisionNote = request.Note;
        _requests.Update(approval);
        await _unitOfWork.SaveAsync(ct);

        return new DecideApprovalResult { Id = approval.Id, Status = approval.Status };
    }
}
