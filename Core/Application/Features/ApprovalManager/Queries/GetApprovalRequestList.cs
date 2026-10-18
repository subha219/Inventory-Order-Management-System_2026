using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ApprovalManager.Queries;

public class ApprovalRequestDto
{
    public string? Id { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? EntityNumber { get; init; }
    public double Units { get; init; }
    public string? Status { get; init; }
    public string? RequestedById { get; init; }
    public DateTime? RequestedAt { get; init; }
    public string? DecidedById { get; init; }
    public DateTime? DecidedAt { get; init; }
    public string? DecisionNote { get; init; }
}

public class GetApprovalRequestListResult
{
    public List<ApprovalRequestDto>? Data { get; init; }
    public int PendingCount { get; init; }
}

public class GetApprovalRequestListRequest : IRequest<GetApprovalRequestListResult>
{
    /// <summary>Pending (default), Approved, Rejected or All.</summary>
    public string? Status { get; init; }
    public int Days { get; init; } = 90;
}

public class GetApprovalRequestListHandler : IRequestHandler<GetApprovalRequestListRequest, GetApprovalRequestListResult>
{
    private readonly IQueryContext _context;

    public GetApprovalRequestListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetApprovalRequestListResult> Handle(GetApprovalRequestListRequest request, CancellationToken ct)
    {
        var status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status;
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(request.Days, 1, 3650));

        var query = _context.ApprovalRequest.AsNoTracking().ApplyIsDeletedFilter(false);
        if (status != "All") query = query.Where(x => x.Status == status);
        if (status != "Pending") query = query.Where(x => x.CreatedAtUtc >= since);

        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).Take(500)
            .Select(x => new ApprovalRequestDto
            {
                Id = x.Id, EntityType = x.EntityType, EntityId = x.EntityId, EntityNumber = x.EntityNumber, Units = x.Units ?? 0,
                Status = x.Status, RequestedById = x.RequestedById, RequestedAt = x.CreatedAtUtc,
                DecidedById = x.DecidedById, DecidedAt = x.DecidedAtUtc, DecisionNote = x.DecisionNote
            })
            .ToListAsync(ct);

        var pending = await _context.ApprovalRequest.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(x => x.Status == "Pending", ct);
        return new GetApprovalRequestListResult { Data = rows, PendingCount = pending };
    }
}
