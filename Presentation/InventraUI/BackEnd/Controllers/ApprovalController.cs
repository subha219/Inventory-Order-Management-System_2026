using Application.Features.ApprovalManager.Commands;
using Application.Features.ApprovalManager.Queries;
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
public class ApprovalController : BaseApiController
{
    private readonly ApprovalSettings _settings;

    public ApprovalController(ISender sender, IOptions<ApprovalSettings> settings) : base(sender)
    {
        _settings = settings.Value;
    }

    private bool IsApprover => _settings.ApproverRoles.Any(User.IsInRole);

    [Authorize]
    [HttpGet("GetApprovalRequestList")]
    public async Task<ActionResult<ApiSuccessResult<GetApprovalRequestListResult>>> GetApprovalRequestListAsync([FromQuery] GetApprovalRequestListRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetApprovalRequestListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetApprovalRequestListAsync)}",
            Content = response
        });
    }

    /// <summary>Tells the page whether to show approve/reject buttons and what the rule is.</summary>
    [Authorize]
    [HttpGet("GetApprovalSettings")]
    public ActionResult<ApiSuccessResult<object>> GetApprovalSettings()
    {
        return Ok(new ApiSuccessResult<object>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetApprovalSettings)}",
            Content = new { enabled = _settings.Enabled, unitsThreshold = _settings.UnitsThreshold, canApprove = IsApprover }
        });
    }

    [Authorize]
    [HttpPost("DecideApproval")]
    public async Task<ActionResult<ApiSuccessResult<DecideApprovalResult>>> DecideApprovalAsync(DecideApprovalRequest request, CancellationToken cancellationToken)
    {
        if (!IsApprover) return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResult { Code = 403, Message = "Only an approver can approve or reject requests." });

        var response = await _sender.Send(new DecideApprovalRequest
        {
            Id = request.Id,
            Approve = request.Approve,
            Note = request.Note,
            DecidedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
        }, cancellationToken);

        return Ok(new ApiSuccessResult<DecideApprovalResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DecideApprovalAsync)}",
            Content = response
        });
    }
}
