using Application.Features.WaveManager.Commands;
using Application.Features.WaveManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class WaveController : BaseApiController
{
    public WaveController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Warehouses")]
    [HttpGet("GetWaves")]
    public async Task<ActionResult<ApiSuccessResult<GetWavesResult>>> GetWavesAsync([FromQuery] GetWavesRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetWavesResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetWavesAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("CreateWave")]
    public async Task<ActionResult<ApiSuccessResult<WaveResult>>> CreateWaveAsync(CreateWaveRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new CreateWaveRequest { DeliveryOrderIds = request.DeliveryOrderIds, Notes = request.Notes, CreatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<WaveResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(CreateWaveAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("ConfirmWavePick")]
    public async Task<ActionResult<ApiSuccessResult<WaveResult>>> ConfirmWavePickAsync(ConfirmWavePickRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ConfirmWavePickRequest { WaveId = request.WaveId, Picks = request.Picks, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<WaveResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ConfirmWavePickAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("CancelWave")]
    public async Task<ActionResult<ApiSuccessResult<WaveResult>>> CancelWaveAsync(CancelWaveRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new CancelWaveRequest { WaveId = request.WaveId, UpdatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<WaveResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(CancelWaveAsync)}", Content = response });
    }
}
