using Application.Features.StockHoldManager.Commands;
using Application.Features.StockHoldManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class StockHoldController : BaseApiController
{
    public StockHoldController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Warehouses")]
    [HttpGet("GetHolds")]
    public async Task<ActionResult<ApiSuccessResult<GetStockHoldsResult>>> GetHoldsAsync(CancellationToken ct)
    {
        var response = await _sender.Send(new GetStockHoldsRequest(), ct);
        return Ok(new ApiSuccessResult<GetStockHoldsResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetHoldsAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("PlaceHold")]
    public async Task<ActionResult<ApiSuccessResult<HoldResult>>> PlaceHoldAsync(PlaceHoldRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new PlaceHoldRequest { ProductId = r.ProductId, WarehouseId = r.WarehouseId, Quantity = r.Quantity, Reason = r.Reason, Notes = r.Notes, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<HoldResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(PlaceHoldAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("ReleaseHold")]
    public async Task<ActionResult<ApiSuccessResult<HoldResult>>> ReleaseHoldAsync(ReleaseHoldRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new ReleaseHoldRequest { Id = r.Id, Quantity = r.Quantity, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<HoldResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ReleaseHoldAsync)}", Content = response });
    }
}
