using Application.Features.ScheduledReportManager.Commands;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using InventraUI.BackEnd.Common.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ScheduledReportController : BaseApiController
{
    private readonly ReportSettings _settings;

    public ScheduledReportController(ISender sender, IOptions<ReportSettings> settings) : base(sender)
    {
        _settings = settings.Value;
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Users")]
    [HttpGet("GetScheduledReports")]
    public async Task<ActionResult<ApiSuccessResult<object>>> GetScheduledReportsAsync(CancellationToken cancellationToken)
    {
        var r = await _sender.Send(new GetScheduledReportsRequest { OffsetMinutes = _settings.TimeZoneOffsetMinutes }, cancellationToken);
        return Ok(new ApiSuccessResult<object>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetScheduledReportsAsync)}",
            Content = new { data = r.Data, types = r.Types, offsetMinutes = _settings.TimeZoneOffsetMinutes }
        });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("SaveScheduledReport")]
    public async Task<ActionResult<ApiSuccessResult<string>>> SaveScheduledReportAsync(SaveScheduledReportRequest request, CancellationToken cancellationToken)
    {
        var id = await _sender.Send(new SaveScheduledReportRequest
        {
            Id = request.Id, Name = request.Name, ReportType = request.ReportType, Frequency = request.Frequency, DayOfWeek = request.DayOfWeek,
            DayOfMonth = request.DayOfMonth, SendHour = request.SendHour, SendMinute = request.SendMinute, Recipients = request.Recipients,
            Enabled = request.Enabled, WarehouseId = request.WarehouseId, SlowDays = request.SlowDays, UpdatedById = UserId
        }, cancellationToken);
        return Ok(new ApiSuccessResult<string> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveScheduledReportAsync)}", Content = id });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("DeleteScheduledReport")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> DeleteScheduledReportAsync(DeleteScheduledReportRequest request, CancellationToken cancellationToken)
    {
        var ok = await _sender.Send(new DeleteScheduledReportRequest { Id = request.Id, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<bool> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeleteScheduledReportAsync)}", Content = ok });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("SendNow")]
    public async Task<ActionResult<ApiSuccessResult<RunScheduledReportsResult>>> SendNowAsync(RunScheduledReportsRequest request, CancellationToken cancellationToken)
    {
        var r = await _sender.Send(new RunScheduledReportsRequest { Id = request.Id, OffsetMinutes = _settings.TimeZoneOffsetMinutes }, cancellationToken);
        return Ok(new ApiSuccessResult<RunScheduledReportsResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SendNowAsync)}", Content = r });
    }
}
