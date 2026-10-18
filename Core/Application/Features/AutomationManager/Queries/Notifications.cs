using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AutomationManager.Queries;

public class NotificationDto
{
    public string? Id { get; init; }
    public string? Severity { get; init; }
    public string? Title { get; init; }
    public string? Message { get; init; }
    public string? Link { get; init; }
    public DateTime? At { get; init; }
    public bool Unread { get; init; }
}

public class GetNotificationsResult
{
    public List<NotificationDto> Data { get; init; } = new();
    public int UnreadCount { get; init; }
}

public class GetNotificationsRequest : IRequest<GetNotificationsResult>
{
    public string? UserId { get; init; }
}

public class GetNotificationsHandler : IRequestHandler<GetNotificationsRequest, GetNotificationsResult>
{
    private readonly IQueryContext _context;

    public GetNotificationsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetNotificationsResult> Handle(GetNotificationsRequest request, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var tag = $";{request.UserId};";
        var rows = await _context.AppNotification.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(n => n.CreatedAtUtc >= since)
            .OrderByDescending(n => n.UpdatedAtUtc ?? n.CreatedAtUtc).Take(30)
            .Select(n => new NotificationDto
            {
                Id = n.Id, Severity = n.Severity, Title = n.Title, Message = n.Message, Link = n.Link,
                At = n.UpdatedAtUtc ?? n.CreatedAtUtc, Unread = n.ReadByIds == null || !n.ReadByIds.Contains(tag)
            }).ToListAsync(ct);
        return new GetNotificationsResult { Data = rows, UnreadCount = rows.Count(r => r.Unread) };
    }
}
