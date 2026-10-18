using Application.Features.StorageLocationManager.Commands;
using Application.Features.StorageLocationManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class StorageLocationController : BaseApiController
{
    public StorageLocationController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetStorageLocationList")]
    public async Task<ActionResult<ApiSuccessResult<GetStorageLocationListResult>>> GetStorageLocationListAsync(
        CancellationToken cancellationToken, [FromQuery] string? warehouseId = null)
    {
        var response = await _sender.Send(new GetStorageLocationListRequest { WarehouseId = warehouseId }, cancellationToken);
        return Ok(new ApiSuccessResult<GetStorageLocationListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetStorageLocationListAsync)}",
            Content = response
        });
    }

    private async Task<ActionResult<ApiSuccessResult<StorageLocationResult>>> Run(IRequest<StorageLocationResult> request, string name, CancellationToken ct)
    {
        var response = await _sender.Send(request, ct);
        return Ok(new ApiSuccessResult<StorageLocationResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {name}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("CreateStorageLocation")]
    public Task<ActionResult<ApiSuccessResult<StorageLocationResult>>> CreateStorageLocationAsync(CreateStorageLocationRequest request, CancellationToken ct)
        => Run(request, nameof(CreateStorageLocationAsync), ct);

    [Authorize]
    [HttpPost("UpdateStorageLocation")]
    public Task<ActionResult<ApiSuccessResult<StorageLocationResult>>> UpdateStorageLocationAsync(UpdateStorageLocationRequest request, CancellationToken ct)
        => Run(request, nameof(UpdateStorageLocationAsync), ct);

    [Authorize]
    [HttpPost("DeleteStorageLocation")]
    public Task<ActionResult<ApiSuccessResult<StorageLocationResult>>> DeleteStorageLocationAsync(DeleteStorageLocationRequest request, CancellationToken ct)
        => Run(request, nameof(DeleteStorageLocationAsync), ct);

    [Authorize]
    [HttpPost("GenerateStorageLocations")]
    public Task<ActionResult<ApiSuccessResult<StorageLocationResult>>> GenerateStorageLocationsAsync(GenerateStorageLocationsRequest request, CancellationToken ct)
        => Run(request, nameof(GenerateStorageLocationsAsync), ct);

    [Authorize]
    [HttpPost("SetLocationStock")]
    public Task<ActionResult<ApiSuccessResult<StorageLocationResult>>> SetLocationStockAsync(SetLocationStockRequest request, CancellationToken ct)
        => Run(request, nameof(SetLocationStockAsync), ct);

    [Authorize]
    [HttpPost("MoveLocationStock")]
    public Task<ActionResult<ApiSuccessResult<StorageLocationResult>>> MoveLocationStockAsync(MoveLocationStockRequest request, CancellationToken ct)
        => Run(request, nameof(MoveLocationStockAsync), ct);
}
