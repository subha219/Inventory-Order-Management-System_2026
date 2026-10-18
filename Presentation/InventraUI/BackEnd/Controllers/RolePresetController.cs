using Application.Features.RolePresetManager.Commands;
using Application.Features.RolePresetManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class RolePresetController : BaseApiController
{
    public RolePresetController(ISender sender) : base(sender)
    {
    }

    [Authorize(Roles = "Users")]
    [HttpGet("GetPresets")]
    public async Task<ActionResult<ApiSuccessResult<GetRolePresetsResult>>> GetPresetsAsync(CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetRolePresetsRequest(), cancellationToken);
        return Ok(new ApiSuccessResult<GetRolePresetsResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetPresetsAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("Apply")]
    public async Task<ActionResult<ApiSuccessResult<ApplyRolePresetResult>>> ApplyAsync(ApplyRolePresetRequest r, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ApplyRolePresetRequest { PresetKey = r.PresetKey, UserId = r.UserId, CreateTestUser = r.CreateTestUser, CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier) }, cancellationToken);
        return Ok(new ApiSuccessResult<ApplyRolePresetResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ApplyAsync)}", Content = response });
    }
}
