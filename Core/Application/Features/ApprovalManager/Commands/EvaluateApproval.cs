using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.ApprovalManager.Commands;

public class EvaluateApprovalResult
{
    public bool RequiresApproval { get; set; }
    /// <summary>Status the document currently has (0 draft, 1 cancelled, 2 confirmed, 3 archived).</summary>
    public int CurrentStatus { get; set; }
    public string? Number { get; set; }
    public double Units { get; set; }
}

public class EvaluateApprovalRequest : IRequest<EvaluateApprovalResult>
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? RequestedById { get; init; }
    public double UnitsThreshold { get; init; }
}

/// <summary>
/// Called when someone without approval rights tries to confirm a document. If the document moves at least the
/// threshold number of units, a pending approval request is recorded (once) and the caller must not confirm it.
/// </summary>
public class EvaluateApprovalHandler : IRequestHandler<EvaluateApprovalRequest, EvaluateApprovalResult>
{
    private readonly ICommandRepository<PositiveAdjustment> _positive;
    private readonly ICommandRepository<NegativeAdjustment> _negative;
    private readonly ICommandRepository<Scrapping> _scrapping;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly ICommandRepository<ApprovalRequest> _requests;
    private readonly IUnitOfWork _unitOfWork;

    public EvaluateApprovalHandler(
        ICommandRepository<PositiveAdjustment> positive,
        ICommandRepository<NegativeAdjustment> negative,
        ICommandRepository<Scrapping> scrapping,
        ICommandRepository<InventoryTransaction> transactions,
        ICommandRepository<ApprovalRequest> requests,
        IUnitOfWork unitOfWork)
    {
        _positive = positive;
        _negative = negative;
        _scrapping = scrapping;
        _transactions = transactions;
        _requests = requests;
        _unitOfWork = unitOfWork;
    }

    public async Task<EvaluateApprovalResult> Handle(EvaluateApprovalRequest request, CancellationToken ct)
    {
        var id = request.EntityId ?? string.Empty;
        int? status;
        string? number;
        switch (request.EntityType)
        {
            case nameof(PositiveAdjustment):
                var p = await _positive.GetAsync(id, ct);
                status = p == null ? null : (int?)p.Status;
                number = p?.Number;
                break;
            case nameof(NegativeAdjustment):
                var n = await _negative.GetAsync(id, ct);
                status = n == null ? null : (int?)n.Status;
                number = n?.Number;
                break;
            case nameof(Scrapping):
                var s = await _scrapping.GetAsync(id, ct);
                status = s == null ? null : (int?)s.Status;
                number = s?.Number;
                break;
            default:
                return new EvaluateApprovalResult();
        }

        if (status == null) return new EvaluateApprovalResult();
        var result = new EvaluateApprovalResult { CurrentStatus = status.Value, Number = number };
        if (status.Value == 2) return result; // already confirmed

        var units = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ModuleId == id)
            .Select(x => x.Movement ?? 0)
            .ToList()
            .Sum(m => Math.Abs(m));
        result.Units = units;
        if (units <= 0 || units < request.UnitsThreshold) return result;

        result.RequiresApproval = true;
        var pending = _requests.GetQuery().ApplyIsDeletedFilter().Any(x => x.EntityId == id && x.Status == "Pending");
        if (!pending)
        {
            await _requests.CreateAsync(new ApprovalRequest
            {
                CreatedById = request.RequestedById,
                EntityType = request.EntityType,
                EntityId = id,
                EntityNumber = number,
                Units = units,
                Status = "Pending",
                RequestedById = request.RequestedById
            }, ct);
            await _unitOfWork.SaveAsync(ct);
        }
        return result;
    }
}
