using Application.Features.ReturnManager.Commands;
using Application.Features.ReturnManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ReturnInspectionController : BaseApiController
{
    public ReturnInspectionController(ISender sender) : base(sender)
    {
    }

    [Authorize(Roles = "Warehouses")]
    [HttpGet("GetReturns")]
    public async Task<ActionResult<ApiSuccessResult<GetReturnsResult>>> GetReturnsAsync([FromQuery] GetReturnsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetReturnsResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetReturnsAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpPost("SaveReturnInspection")]
    public async Task<ActionResult<ApiSuccessResult<SaveReturnInspectionResult>>> SaveReturnInspectionAsync(SaveReturnInspectionRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new SaveReturnInspectionRequest
        {
            SalesReturnId = request.SalesReturnId, Lines = request.Lines, UpdatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
        }, cancellationToken);
        return Ok(new ApiSuccessResult<SaveReturnInspectionResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveReturnInspectionAsync)}", Content = response });
    }
}
