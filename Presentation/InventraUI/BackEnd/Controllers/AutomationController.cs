using Application.Features.AutomationManager.Commands;
using Application.Features.AutomationManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class AutomationController : BaseApiController
{
    public AutomationController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ---- rules: administrators only ----
    [Authorize(Roles = "Users")]
    [HttpGet("GetAutomationRules")]
    public async Task<ActionResult<ApiSuccessResult<GetAutomationRulesResult>>> GetAutomationRulesAsync(CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetAutomationRulesRequest(), cancellationToken);
        return Ok(new ApiSuccessResult<GetAutomationRulesResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetAutomationRulesAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("SaveAutomationRule")]
    public async Task<ActionResult<ApiSuccessResult<SaveAutomationRuleResult>>> SaveAutomationRuleAsync(SaveAutomationRuleRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new SaveAutomationRuleRequest
        {
            Id = request.Id, Enabled = request.Enabled, Threshold = request.Threshold, NotifyInApp = request.NotifyInApp,
            EmailRecipients = request.EmailRecipients, UpdatedById = UserId
        }, cancellationToken);
        return Ok(new ApiSuccessResult<SaveAutomationRuleResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveAutomationRuleAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("RunNow")]
    public async Task<ActionResult<ApiSuccessResult<RunAutomationRulesResult>>> RunNowAsync(CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new RunAutomationRulesRequest(), cancellationToken);
        return Ok(new ApiSuccessResult<RunAutomationRulesResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(RunNowAsync)}", Content = response });
    }

    // ---- bell menu: every signed-in user ----
    [Authorize]
    [HttpGet("GetNotifications")]
    public async Task<ActionResult<ApiSuccessResult<GetNotificationsResult>>> GetNotificationsAsync(CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetNotificationsRequest { UserId = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<GetNotificationsResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetNotificationsAsync)}", Content = response });
    }

    [Authorize]
    [HttpPost("MarkNotificationsRead")]
    public async Task<ActionResult<ApiSuccessResult<int>>> MarkNotificationsReadAsync(MarkNotificationsReadRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new MarkNotificationsReadRequest { Ids = request.Ids, UserId = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<int> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(MarkNotificationsReadAsync)}", Content = response });
    }
}
