using Application.Features.PickListManager.Commands;
using Application.Features.PickListManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class PickListController : BaseApiController
{
    public PickListController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetPickList")]
    public async Task<ActionResult<ApiSuccessResult<GetPickListResult>>> GetPickListAsync([FromQuery] GetPickListRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetPickListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPickListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("ConfirmPick")]
    public async Task<ActionResult<ApiSuccessResult<ConfirmPickResult>>> ConfirmPickAsync(ConfirmPickRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ConfirmPickRequest
        {
            DeliveryOrderId = request.DeliveryOrderId,
            Picks = request.Picks,
            BatchPicks = request.BatchPicks,
            UpdatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
        }, cancellationToken);
        return Ok(new ApiSuccessResult<ConfirmPickResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ConfirmPickAsync)}",
            Content = response
        });
    }
}
