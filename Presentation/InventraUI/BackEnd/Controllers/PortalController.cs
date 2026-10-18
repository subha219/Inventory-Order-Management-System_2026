using Application.Features.PortalManager.Commands;
using Application.Features.PortalManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

/// <summary>Order portals for vendors and customers. The staff calls need a login; the partner calls only need the secret link token.</summary>
[Route("api/portal")]
public class PortalController : BaseApiController
{
    public PortalController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "PurchaseOrders,SalesOrders")]
    [HttpGet("admin")]
    public async Task<ActionResult<ApiSuccessResult<GetPortalAdminResult>>> AdminAsync(CancellationToken ct)
    {
        var response = await _sender.Send(new GetPortalAdminRequest(), ct);
        return Ok(new ApiSuccessResult<GetPortalAdminResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(AdminAsync)}", Content = response });
    }

    [Authorize(Roles = "PurchaseOrders,SalesOrders")]
    [HttpPost("createlink")]
    public async Task<ActionResult<ApiSuccessResult<PortalResult>>> CreateLinkAsync(CreatePortalLinkRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new CreatePortalLinkRequest { EntityType = r.EntityType, EntityId = r.EntityId, ExpiresInDays = r.ExpiresInDays <= 0 ? 30 : r.ExpiresInDays, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<PortalResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(CreateLinkAsync)}", Content = response });
    }

    [Authorize(Roles = "PurchaseOrders,SalesOrders")]
    [HttpPost("deactivate")]
    public async Task<ActionResult<ApiSuccessResult<PortalResult>>> DeactivateAsync(DeactivatePortalLinkRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new DeactivatePortalLinkRequest { Id = r.Id, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<PortalResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeactivateAsync)}", Content = response });
    }

    [AllowAnonymous]
    [HttpGet("view/{token}")]
    public async Task<ActionResult<ApiSuccessResult<PortalViewResult>>> ViewAsync(string token, CancellationToken ct)
    {
        var response = await _sender.Send(new GetPortalViewRequest { Token = token }, ct);
        if (!response.Found) return NotFound(new ApiSuccessResult<PortalViewResult> { Code = StatusCodes.Status404NotFound, Message = "Link not found.", Content = response });
        return Ok(new ApiSuccessResult<PortalViewResult> { Code = StatusCodes.Status200OK, Message = "OK", Content = response });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("respond/{token}")]
    public async Task<ActionResult<ApiSuccessResult<PortalResult>>> RespondAsync(string token, SubmitPortalResponseRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new SubmitPortalResponseRequest { Token = token, Status = r.Status, ExpectedDate = r.ExpectedDate, Message = r.Message }, ct);
        return Ok(new ApiSuccessResult<PortalResult> { Code = StatusCodes.Status200OK, Message = "OK", Content = response });
    }
}
