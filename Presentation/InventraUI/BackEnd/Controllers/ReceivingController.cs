using Application.Features.ReceivingManager.Commands;
using Application.Features.ReceivingManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ReceivingController : BaseApiController
{
    public ReceivingController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetReceiving")]
    public async Task<ActionResult<ApiSuccessResult<GetReceivingResult>>> GetReceivingAsync([FromQuery] GetReceivingRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetReceivingResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetReceivingAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("SaveInspection")]
    public async Task<ActionResult<ApiSuccessResult<ReceivingResult>>> SaveInspectionAsync(SaveInspectionRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new SaveInspectionRequest
        {
            GoodsReceiveId = request.GoodsReceiveId,
            Lines = request.Lines,
            UpdatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
        }, cancellationToken);
        return Ok(new ApiSuccessResult<ReceivingResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(SaveInspectionAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("ConfirmPutAway")]
    public async Task<ActionResult<ApiSuccessResult<ReceivingResult>>> ConfirmPutAwayAsync(ConfirmPutAwayRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ConfirmPutAwayRequest
        {
            GoodsReceiveId = request.GoodsReceiveId,
            Lines = request.Lines,
            UpdatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
        }, cancellationToken);
        return Ok(new ApiSuccessResult<ReceivingResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ConfirmPutAwayAsync)}",
            Content = response
        });
    }
}
