using Application.Features.TaskManager.Commands;
using Application.Features.TaskManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class TaskController : BaseApiController
{
    public TaskController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Warehouses")]
    [HttpGet("GetTasks")]
    public async Task<ActionResult<ApiSuccessResult<GetTasksResult>>> GetTasksAsync([FromQuery] GetTasksRequest request, CancellationToken ct)
    {
        var response = await _sender.Send(request, ct);
        return Ok(new ApiSuccessResult<GetTasksResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetTasksAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("SaveTask")]
    public async Task<ActionResult<ApiSuccessResult<TaskResult>>> SaveTaskAsync(SaveTaskRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new SaveTaskRequest
        {
            Id = r.Id, TaskType = r.TaskType, Title = r.Title, Reference = r.Reference, WarehouseId = r.WarehouseId, Priority = r.Priority,
            AssignedToId = r.AssignedToId, DueAtUtc = r.DueAtUtc, Notes = r.Notes, UserId = UserId
        }, ct);
        return Ok(new ApiSuccessResult<TaskResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveTaskAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("Act")]
    public async Task<ActionResult<ApiSuccessResult<TaskResult>>> ActAsync(TaskActionRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new TaskActionRequest { Id = r.Id, Action = r.Action, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<TaskResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ActAsync)}", Content = response });
    }
}
