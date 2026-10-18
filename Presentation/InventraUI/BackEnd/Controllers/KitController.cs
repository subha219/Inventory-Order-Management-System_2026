using Application.Features.KitManager.Commands;
using Application.Features.KitManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class KitController : BaseApiController
{
    public KitController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Products")]
    [HttpGet("GetKits")]
    public async Task<ActionResult<ApiSuccessResult<GetKitsResult>>> GetKitsAsync([FromQuery] GetKitsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetKitsResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetKitsAsync)}", Content = response });
    }

    [Authorize(Roles = "Products")]
    [HttpPost("SaveBom")]
    public async Task<ActionResult<ApiSuccessResult<KitResult>>> SaveBomAsync(SaveBomRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new SaveBomRequest { KitProductId = request.KitProductId, Lines = request.Lines, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<KitResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveBomAsync)}", Content = response });
    }

    [Authorize(Roles = "Products")]
    [HttpPost("DeleteBom")]
    public async Task<ActionResult<ApiSuccessResult<KitResult>>> DeleteBomAsync(DeleteBomRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new DeleteBomRequest { KitProductId = request.KitProductId, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<KitResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeleteBomAsync)}", Content = response });
    }

    [Authorize(Roles = "Products")]
    [HttpPost("BuildKit")]
    public async Task<ActionResult<ApiSuccessResult<KitResult>>> BuildKitAsync(BuildKitRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new BuildKitRequest { KitProductId = request.KitProductId, WarehouseId = request.WarehouseId, Quantity = request.Quantity, Notes = request.Notes, CreatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<KitResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(BuildKitAsync)}", Content = response });
    }
}
