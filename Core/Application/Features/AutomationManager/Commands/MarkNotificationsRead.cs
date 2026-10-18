using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.AutomationManager.Commands;

public class MarkNotificationsReadRequest : IRequest<int>
{
    /// <summary>Notification ids to mark; leave empty to mark everything.</summary>
    public List<string>? Ids { get; init; }
    public string? UserId { get; init; }
}

public class MarkNotificationsReadHandler : IRequestHandler<MarkNotificationsReadRequest, int>
{
    private readonly ICommandRepository<AppNotification> _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationsReadHandler(ICommandRepository<AppNotification> notifications, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(MarkNotificationsReadRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.UserId)) return 0;
        var tag = $";{request.UserId};";
        var since = DateTime.UtcNow.AddDays(-30);
        var query = _notifications.GetQuery().ApplyIsDeletedFilter().Where(n => n.CreatedAtUtc >= since);
        var ids = request.Ids?.Where(i => !string.IsNullOrEmpty(i)).ToList();
        if (ids != null && ids.Count > 0) query = query.Where(n => ids.Contains(n.Id));

        var count = 0;
        foreach (var n in query.ToList())
        {
            if (n.ReadByIds != null && n.ReadByIds.Contains(tag)) continue;
            var next = (n.ReadByIds ?? ";") + request.UserId + ";";
            if (next.Length > 3900) next = ";" + request.UserId + ";"; // very busy inbox: start the list over rather than overflow
            n.ReadByIds = next;
            _notifications.Update(n);
            count++;
        }
        if (count > 0) await _unitOfWork.SaveAsync(ct);
        return count;
    }
}
