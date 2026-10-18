using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AuditLogManager.Queries;

public class AuditLogDto
{
    public string? Id { get; init; }
    public DateTime? Timestamp { get; init; }
    public string? UserId { get; init; }
    public string? Action { get; init; }
    public string? EntityName { get; init; }
    public string? EntityId { get; init; }
    public string? EntityLabel { get; init; }
    public string? Changes { get; init; }
}

public class GetAuditLogListResult
{
    public List<AuditLogDto>? Data { get; init; }
    public List<string>? EntityNames { get; init; }
    public bool Truncated { get; init; }
}

public class GetAuditLogListRequest : IRequest<GetAuditLogListResult>
{
    public int Days { get; init; } = 7;
    public string? EntityName { get; init; }
    public string? Action { get; init; }
    public string? Search { get; init; }
    public string? UserId { get; init; }
}

public class GetAuditLogListHandler : IRequestHandler<GetAuditLogListRequest, GetAuditLogListResult>
{
    private const int MaxRows = 500;
    private readonly IQueryContext _context;

    public GetAuditLogListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetAuditLogListResult> Handle(GetAuditLogListRequest request, CancellationToken cancellationToken)
    {
        var days = Math.Clamp(request.Days <= 0 ? 7 : request.Days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-days);

        var query = _context.AuditLog.AsNoTracking().ApplyIsDeletedFilter(false).Where(x => x.CreatedAtUtc >= since);
        if (!string.IsNullOrWhiteSpace(request.EntityName)) query = query.Where(x => x.EntityName == request.EntityName);
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(x => x.Action == request.Action);
        if (!string.IsNullOrWhiteSpace(request.UserId)) query = query.Where(x => x.UserId == request.UserId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x => (x.EntityLabel != null && x.EntityLabel.Contains(s)) ||
                                     (x.EntityName != null && x.EntityName.Contains(s)) ||
                                     (x.Changes != null && x.Changes.Contains(s)));
        }

        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).Take(MaxRows + 1)
            .Select(x => new AuditLogDto
            {
                Id = x.Id, Timestamp = x.CreatedAtUtc, UserId = x.UserId, Action = x.Action,
                EntityName = x.EntityName, EntityId = x.EntityId, EntityLabel = x.EntityLabel, Changes = x.Changes
            })
            .ToListAsync(cancellationToken);

        var names = await _context.AuditLog.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.CreatedAtUtc >= since && x.EntityName != null)
            .Select(x => x.EntityName!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);

        return new GetAuditLogListResult
        {
            Data = rows.Take(MaxRows).ToList(),
            EntityNames = names,
            Truncated = rows.Count > MaxRows
        };
    }
}
