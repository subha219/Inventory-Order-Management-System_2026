using Application.Features.ShippingManager.Commands;
using Application.Features.ShippingManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ShippingController : BaseApiController
{
    public ShippingController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Warehouses")]
    [HttpGet("GetPacking")]
    public async Task<ActionResult<ApiSuccessResult<GetPackingResult>>> GetPackingAsync([FromQuery] GetPackingRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetPackingResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetPackingAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("CreateShipment")]
    public async Task<ActionResult<ApiSuccessResult<ShippingResult>>> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new CreateShipmentRequest { DeliveryOrderId = request.DeliveryOrderId, CreatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<ShippingResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(CreateShipmentAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("SavePackages")]
    public async Task<ActionResult<ApiSuccessResult<ShippingResult>>> SavePackagesAsync(SavePackagesRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new SavePackagesRequest { ShipmentId = request.ShipmentId, Packages = request.Packages, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<ShippingResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SavePackagesAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("ShipShipment")]
    public async Task<ActionResult<ApiSuccessResult<ShippingResult>>> ShipShipmentAsync(ShipShipmentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ShipShipmentRequest { ShipmentId = request.ShipmentId, Carrier = request.Carrier, TrackingNumber = request.TrackingNumber, Notes = request.Notes, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<ShippingResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ShipShipmentAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("DeleteShipment")]
    public async Task<ActionResult<ApiSuccessResult<ShippingResult>>> DeleteShipmentAsync(DeleteShipmentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new DeleteShipmentRequest { ShipmentId = request.ShipmentId, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<ShippingResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeleteShipmentAsync)}", Content = response });
    }
}
