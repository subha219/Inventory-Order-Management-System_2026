using Application.Features.ProductSerialManager.Commands;
using Application.Features.ProductSerialManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ProductSerialController : BaseApiController
{
    public ProductSerialController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize]
    [HttpGet("GetSerialList")]
    public async Task<ActionResult<ApiSuccessResult<GetSerialListResult>>> GetSerialListAsync([FromQuery] GetSerialListRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetSerialListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSerialListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("RegisterSerials")]
    public async Task<ActionResult<ApiSuccessResult<SerialResult>>> RegisterSerialsAsync(RegisterSerialsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new RegisterSerialsRequest
        {
            ProductId = request.ProductId, WarehouseId = request.WarehouseId, LocationId = request.LocationId,
            GoodsReceiveId = request.GoodsReceiveId, Serials = request.Serials, Notes = request.Notes, UpdatedById = UserId
        }, cancellationToken);
        return Ok(new ApiSuccessResult<SerialResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(RegisterSerialsAsync)}", Content = response });
    }

    [Authorize]
    [HttpPost("ChangeSerialStatus")]
    public async Task<ActionResult<ApiSuccessResult<SerialResult>>> ChangeSerialStatusAsync(ChangeSerialStatusRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ChangeSerialStatusRequest
        {
            Id = request.Id, Status = request.Status, DeliveryOrderId = request.DeliveryOrderId, Notes = request.Notes, UpdatedById = UserId
        }, cancellationToken);
        return Ok(new ApiSuccessResult<SerialResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ChangeSerialStatusAsync)}", Content = response });
    }

    [Authorize]
    [HttpPost("DeleteSerial")]
    public async Task<ActionResult<ApiSuccessResult<SerialResult>>> DeleteSerialAsync(DeleteSerialRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new DeleteSerialRequest { Id = request.Id, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<SerialResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeleteSerialAsync)}", Content = response });
    }
}
