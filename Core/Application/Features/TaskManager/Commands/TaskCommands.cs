using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.TaskManager.Commands;

public class TaskResult
{
    public string? Id { get; set; }
    public string? Message { get; set; }
}

public class SaveTaskRequest : IRequest<TaskResult>
{
    public string? Id { get; init; }
    public string? TaskType { get; init; }
    public string? Title { get; init; }
    public string? Reference { get; init; }
    public string? WarehouseId { get; init; }
    public string? Priority { get; init; }
    public string? AssignedToId { get; init; }
    public DateTime? DueAtUtc { get; init; }
    public string? Notes { get; init; }
    public string? UserId { get; init; }
}

public class SaveTaskValidator : AbstractValidator<SaveTaskRequest>
{
    private static readonly string[] Types = { "Pick", "PutAway", "Count", "Move", "Receive", "Pack", "Other" };
    private static readonly string[] Priorities = { "Low", "Normal", "High", "Urgent" };

    public SaveTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Describe the task.").MaximumLength(200);
        RuleFor(x => x.TaskType).Must(t => t != null && Types.Contains(t)).WithMessage("Choose a task type.");
        RuleFor(x => x.Priority).Must(t => t != null && Priorities.Contains(t)).WithMessage("Choose a priority.");
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class SaveTaskHandler : IRequestHandler<SaveTaskRequest, TaskResult>
{
    private readonly ICommandRepository<WarehouseTask> _tasks;
    private readonly NumberSequenceService _numbers;
    private readonly IUnitOfWork _unitOfWork;

    public SaveTaskHandler(ICommandRepository<WarehouseTask> tasks, NumberSequenceService numbers, IUnitOfWork unitOfWork)
    {
        _tasks = tasks; _numbers = numbers; _unitOfWork = unitOfWork;
    }

    public async Task<TaskResult> Handle(SaveTaskRequest r, CancellationToken ct)
    {
        WarehouseTask? t = string.IsNullOrEmpty(r.Id) ? null : await _tasks.GetAsync(r.Id, ct);
        if (t == null)
        {
            t = new WarehouseTask { Number = _numbers.GenerateNumber(nameof(WarehouseTask), "", "TSK"), Status = "Open", CreatedById = r.UserId };
            _tasks.Create(t);
        }
        else
        {
            if (t.Status != "Open" && t.Status != "InProgress") throw new Exception("A finished task cannot be changed.");
            t.UpdatedById = r.UserId;
        }
        t.TaskType = r.TaskType; t.Title = r.Title?.Trim(); t.Reference = r.Reference?.Trim(); t.WarehouseId = string.IsNullOrEmpty(r.WarehouseId) ? null : r.WarehouseId;
        t.Priority = r.Priority; t.AssignedToId = string.IsNullOrEmpty(r.AssignedToId) ? null : r.AssignedToId;
        t.DueAtUtc = r.DueAtUtc == null ? null : DateTime.SpecifyKind(r.DueAtUtc.Value, DateTimeKind.Utc); t.Notes = r.Notes?.Trim();
        if (t.UpdatedById != null) _tasks.Update(t);
        await _unitOfWork.SaveAsync(ct);
        return new TaskResult { Id = t.Id, Message = t.Number + " saved." };
    }
}

public class TaskActionRequest : IRequest<TaskResult>
{
    public string? Id { get; init; }
    /// <summary>Start, Complete, Cancel, Reopen.</summary>
    public string? Action { get; init; }
    public string? UserId { get; init; }
}

public class TaskActionHandler : IRequestHandler<TaskActionRequest, TaskResult>
{
    private readonly ICommandRepository<WarehouseTask> _tasks;
    private readonly IUnitOfWork _unitOfWork;

    public TaskActionHandler(ICommandRepository<WarehouseTask> tasks, IUnitOfWork unitOfWork)
    {
        _tasks = tasks; _unitOfWork = unitOfWork;
    }

    public async Task<TaskResult> Handle(TaskActionRequest r, CancellationToken ct)
    {
        var t = await _tasks.GetAsync(r.Id ?? string.Empty, ct) ?? throw new Exception("Task not found.");
        switch (r.Action)
        {
            case "Start":
                if (t.Status != "Open") throw new Exception("Only an open task can be started.");
                t.Status = "InProgress"; t.StartedAtUtc = DateTime.UtcNow;
                if (string.IsNullOrEmpty(t.AssignedToId)) t.AssignedToId = r.UserId; // picking up an unassigned task claims it
                break;
            case "Complete":
                if (t.Status != "Open" && t.Status != "InProgress") throw new Exception("This task is already closed.");
                t.StartedAtUtc ??= DateTime.UtcNow;
                if (string.IsNullOrEmpty(t.AssignedToId)) t.AssignedToId = r.UserId;
                t.Status = "Done"; t.CompletedAtUtc = DateTime.UtcNow; break;
            case "Cancel":
                if (t.Status != "Open" && t.Status != "InProgress") throw new Exception("This task is already closed.");
                t.Status = "Cancelled"; break;
            case "Reopen":
                if (t.Status != "Done" && t.Status != "Cancelled") throw new Exception("Only a finished task can be reopened.");
                t.Status = "Open"; t.StartedAtUtc = null; t.CompletedAtUtc = null; break;
            default: throw new Exception("Unknown action.");
        }
        t.UpdatedById = r.UserId;
        _tasks.Update(t);
        await _unitOfWork.SaveAsync(ct);
        return new TaskResult { Id = t.Id, Message = t.Number + " is now " + t.Status + "." };
    }
}
