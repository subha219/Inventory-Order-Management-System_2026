using Application.Features.RecallManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class RecallController : BaseApiController
{
    public RecallController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetRecallTrace")]
    public async Task<ActionResult<ApiSuccessResult<GetRecallTraceResult>>> GetRecallTraceAsync([FromQuery] GetRecallTraceRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetRecallTraceResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetRecallTraceAsync)}",
            Content = response
        });
    }
}
