using Application.Features.BackorderManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class BackorderController : BaseApiController
{
    public BackorderController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetBackorders")]
    public async Task<ActionResult<ApiSuccessResult<GetBackordersResult>>> GetBackordersAsync([FromQuery] GetBackordersRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetBackordersResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetBackordersAsync)}",
            Content = response
        });
    }
}
