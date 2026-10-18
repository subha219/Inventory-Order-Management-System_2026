using Application.Features.AuditLogManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class AuditLogController : BaseApiController
{
    public AuditLogController(ISender sender) : base(sender)
    {
    }

    [Authorize(Roles = "Users,Auditor / Read-only")]
    [HttpGet("GetAuditLogList")]
    public async Task<ActionResult<ApiSuccessResult<GetAuditLogListResult>>> GetAuditLogListAsync(
        CancellationToken cancellationToken,
        [FromQuery] int days = 7,
        [FromQuery] string? entityName = null,
        [FromQuery] string? action = null,
        [FromQuery] string? search = null,
        [FromQuery] string? userId = null)
    {
        var response = await _sender.Send(new GetAuditLogListRequest
        {
            Days = days, EntityName = entityName, Action = action, Search = search, UserId = userId
        }, cancellationToken);

        return Ok(new ApiSuccessResult<GetAuditLogListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetAuditLogListAsync)}",
            Content = response
        });
    }
}
