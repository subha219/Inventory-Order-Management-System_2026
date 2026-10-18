using Application.Features.ProductManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class StockAvailabilityController : BaseApiController
{
    public StockAvailabilityController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetStockAvailability")]
    public async Task<ActionResult<ApiSuccessResult<GetStockAvailabilityResult>>> GetStockAvailabilityAsync([FromQuery] GetStockAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetStockAvailabilityResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetStockAvailabilityAsync)}",
            Content = response
        });
    }
}
