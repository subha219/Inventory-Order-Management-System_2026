using Application.Features.CostingManager.Commands;
using Application.Features.CostingManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class CostingController : BaseApiController
{
    public CostingController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "StockReports")]
    [HttpGet("GetCosting")]
    public async Task<ActionResult<ApiSuccessResult<GetCostingResult>>> GetCostingAsync([FromQuery] GetCostingRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetCostingResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetCostingAsync)}", Content = response });
    }

    [Authorize(Roles = "StockReports")]
    [HttpPost("SaveLandedCost")]
    public async Task<ActionResult<ApiSuccessResult<LandedCostResult>>> SaveLandedCostAsync(SaveLandedCostRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new SaveLandedCostRequest { GoodsReceiveId = request.GoodsReceiveId, Description = request.Description, Amount = request.Amount, Basis = request.Basis, CreatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<LandedCostResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveLandedCostAsync)}", Content = response });
    }

    [Authorize(Roles = "StockReports")]
    [HttpPost("DeleteLandedCost")]
    public async Task<ActionResult<ApiSuccessResult<LandedCostResult>>> DeleteLandedCostAsync(DeleteLandedCostRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new DeleteLandedCostRequest { Id = request.Id, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<LandedCostResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeleteLandedCostAsync)}", Content = response });
    }
}
