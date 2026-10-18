using Application.Features.BinMoveManager.Commands;
using Application.Features.BinMoveManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class BinMoveController : BaseApiController
{
    public BinMoveController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Warehouses")]
    [HttpGet("GetBinMoves")]
    public async Task<ActionResult<ApiSuccessResult<GetBinMovesResult>>> GetBinMovesAsync([FromQuery] GetBinMovesRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetBinMovesResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetBinMovesAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("MoveStock")]
    public async Task<ActionResult<ApiSuccessResult<BinMoveResult>>> MoveStockAsync(MoveStockRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new MoveStockRequest
        {
            ProductId = request.ProductId, FromLocationId = request.FromLocationId, ToLocationId = request.ToLocationId, Quantity = request.Quantity,
            Reason = request.Reason, MoveType = request.MoveType, UpdatedById = UserId
        }, cancellationToken);
        return Ok(new ApiSuccessResult<BinMoveResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(MoveStockAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("SaveReplenishmentRule")]
    public async Task<ActionResult<ApiSuccessResult<BinMoveResult>>> SaveReplenishmentRuleAsync(SaveReplenishmentRuleRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new SaveReplenishmentRuleRequest { LocationId = request.LocationId, ProductId = request.ProductId, Min = request.Min, Max = request.Max, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<BinMoveResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveReplenishmentRuleAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("DeleteReplenishmentRule")]
    public async Task<ActionResult<ApiSuccessResult<BinMoveResult>>> DeleteReplenishmentRuleAsync(DeleteReplenishmentRuleRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new DeleteReplenishmentRuleRequest { Id = request.Id, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<BinMoveResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeleteReplenishmentRuleAsync)}", Content = response });
    }
}
