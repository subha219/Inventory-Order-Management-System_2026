using Application.Features.DockManager.Commands;
using Application.Features.DockManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class DockController : BaseApiController
{
    public DockController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Warehouses")]
    [HttpGet("GetSchedule")]
    public async Task<ActionResult<ApiSuccessResult<GetDockScheduleResult>>> GetScheduleAsync([FromQuery] GetDockScheduleRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetDockScheduleResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetScheduleAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("SaveDoor")]
    public async Task<ActionResult<ApiSuccessResult<DockResult>>> SaveDoorAsync(SaveDockDoorRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new SaveDockDoorRequest { Id = r.Id, Name = r.Name, WarehouseId = r.WarehouseId, DoorType = r.DoorType, IsActive = r.IsActive, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<DockResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveDoorAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("DeleteDoor")]
    public async Task<ActionResult<ApiSuccessResult<DockResult>>> DeleteDoorAsync(DeleteDockDoorRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new DeleteDockDoorRequest { Id = r.Id, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<DockResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeleteDoorAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("SaveAppointment")]
    public async Task<ActionResult<ApiSuccessResult<DockResult>>> SaveAppointmentAsync(SaveDockAppointmentRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new SaveDockAppointmentRequest
        {
            Id = r.Id, DockDoorId = r.DockDoorId, Direction = r.Direction, StartAtUtc = r.StartAtUtc, DurationMinutes = r.DurationMinutes,
            Carrier = r.Carrier, Reference = r.Reference, PartyName = r.PartyName, Notes = r.Notes, UserId = UserId
        }, ct);
        return Ok(new ApiSuccessResult<DockResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveAppointmentAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("SetStatus")]
    public async Task<ActionResult<ApiSuccessResult<DockResult>>> SetStatusAsync(SetDockAppointmentStatusRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new SetDockAppointmentStatusRequest { Id = r.Id, Action = r.Action, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<DockResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SetStatusAsync)}", Content = response });
    }
}
