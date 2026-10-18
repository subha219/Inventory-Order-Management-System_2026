using Application.Features.CycleCountManager.Commands;
using Application.Features.CycleCountManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class CycleCountController : BaseApiController
{
    public CycleCountController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetCycleCountPlan")]
    public async Task<ActionResult<ApiSuccessResult<GetCycleCountPlanResult>>> GetCycleCountPlanAsync([FromQuery] GetCycleCountPlanRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetCycleCountPlanResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetCycleCountPlanAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("CreateCycleCountSheet")]
    public async Task<ActionResult<ApiSuccessResult<CreateCycleCountSheetResult>>> CreateCycleCountSheetAsync(CreateCycleCountSheetRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new CreateCycleCountSheetRequest
        {
            WarehouseId = request.WarehouseId,
            ProductIds = request.ProductIds,
            Description = request.Description,
            CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
        }, cancellationToken);
        return Ok(new ApiSuccessResult<CreateCycleCountSheetResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateCycleCountSheetAsync)}",
            Content = response
        });
    }
}
