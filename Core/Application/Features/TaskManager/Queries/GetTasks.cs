using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TaskManager.Queries;

public class TaskDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? TaskType { get; init; }
    public string? Title { get; init; }
    public string? Reference { get; init; }
    public string? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string? Priority { get; init; }
    public string? AssignedToId { get; init; }
    public string? Status { get; init; }
    public DateTime? DueAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public string? Notes { get; init; }
    public bool Overdue { get; init; }
}

public class WorkerStatDto
{
    public string? UserId { get; init; }
    public int Done { get; init; }
    public int Open { get; init; }
    public double? AvgMinutes { get; init; }
    public double? OnTimePercent { get; init; }
}

public class TaskWarehouseDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class GetTasksResult
{
    public List<TaskDto> Tasks { get; init; } = new();
    public List<WorkerStatDto> Workers { get; init; } = new();
    public List<TaskWarehouseDto> Warehouses { get; init; } = new();
}

public class GetTasksRequest : IRequest<GetTasksResult>
{
    /// <summary>Productivity window in days.</summary>
    public int Days { get; init; } = 30;
}

public class GetTasksHandler : IRequestHandler<GetTasksRequest, GetTasksResult>
{
    private readonly IQueryContext _context;

    public GetTasksHandler(IQueryContext context)
    {
        _context = context;
    }

    private static DateTime? Utc(DateTime? d) => d == null ? null : DateTime.SpecifyKind(d.Value, DateTimeKind.Utc);

    public async Task<GetTasksResult> Handle(GetTasksRequest r, CancellationToken ct)
    {
        var days = Math.Clamp(r.Days, 7, 365);
        var since = DateTime.UtcNow.Date.AddDays(-days);
        var recent = DateTime.UtcNow.Date.AddDays(-7);
        var warehouses = await _context.Warehouse.AsNoTracking().ApplyIsDeletedFilter(false).Where(w => w.SystemWarehouse == false).OrderBy(w => w.Name)
            .Select(w => new TaskWarehouseDto { Id = w.Id, Name = w.Name }).ToListAsync(ct);
        var whName = warehouses.ToDictionary(w => w.Id ?? "", w => w.Name);

        var rows = await _context.WarehouseTask.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(t => t.Status == "Open" || t.Status == "InProgress" || t.CompletedAtUtc >= recent || t.UpdatedAtUtc >= recent)
            .OrderBy(t => t.Status == "Done" || t.Status == "Cancelled" ? 1 : 0).ThenByDescending(t => t.CreatedAtUtc).Take(400).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var tasks = rows.Select(t => new TaskDto
        {
            Id = t.Id, Number = t.Number, TaskType = t.TaskType, Title = t.Title, Reference = t.Reference, WarehouseId = t.WarehouseId,
            WarehouseName = whName.TryGetValue(t.WarehouseId ?? "", out var wn) ? wn : "", Priority = t.Priority, AssignedToId = t.AssignedToId, Status = t.Status,
            DueAtUtc = Utc(t.DueAtUtc), StartedAtUtc = Utc(t.StartedAtUtc), CompletedAtUtc = Utc(t.CompletedAtUtc), CreatedAtUtc = Utc(t.CreatedAtUtc), Notes = t.Notes,
            Overdue = (t.Status == "Open" || t.Status == "InProgress") && t.DueAtUtc != null && t.DueAtUtc < now
        }).ToList();

        var finished = await _context.WarehouseTask.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(t => t.Status == "Done" && t.CompletedAtUtc >= since && t.AssignedToId != null).ToListAsync(ct);
        var open = tasks.Where(t => (t.Status == "Open" || t.Status == "InProgress") && t.AssignedToId != null).GroupBy(t => t.AssignedToId).ToDictionary(g => g.Key!, g => g.Count());
        var workers = finished.GroupBy(t => t.AssignedToId).Select(g =>
        {
            var mins = g.Where(t => t.StartedAtUtc != null && t.CompletedAtUtc != null).Select(t => (t.CompletedAtUtc!.Value - t.StartedAtUtc!.Value).TotalMinutes).Where(m => m >= 0).ToList();
            var due = g.Where(t => t.DueAtUtc != null).ToList();
            return new WorkerStatDto
            {
                UserId = g.Key, Done = g.Count(), Open = open.TryGetValue(g.Key!, out var o) ? o : 0, AvgMinutes = mins.Count > 0 ? Math.Round(mins.Average(), 1) : null,
                OnTimePercent = due.Count > 0 ? Math.Round(100.0 * due.Count(t => t.CompletedAtUtc <= t.DueAtUtc) / due.Count, 0) : null
            };
        }).OrderByDescending(w => w.Done).ToList();
        foreach (var uid in open.Keys.Where(k => workers.All(w => w.UserId != k)))
            workers.Add(new WorkerStatDto { UserId = uid, Done = 0, Open = open[uid] });

        return new GetTasksResult { Tasks = tasks, Workers = workers, Warehouses = warehouses };
    }
}
