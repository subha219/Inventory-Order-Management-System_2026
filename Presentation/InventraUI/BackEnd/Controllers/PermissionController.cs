using System.Security.Claims;
using Application.Features.PermissionManager;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using InventraUI.BackEnd.Common.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class PermissionController : BaseApiController
{
    private static readonly string[] Hidden = { "Security", "Permission", "Token", "Approval" };
    private readonly IActionDescriptorCollectionProvider _actions;
    private readonly IMemoryCache _cache;

    public PermissionController(ISender sender, IActionDescriptorCollectionProvider actions, IMemoryCache cache) : base(sender)
    {
        _actions = actions;
        _cache = cache;
    }

    /// <summary>Every module (API controller) that has create, update or delete actions, with the actions each one offers, and what the chosen role is denied.</summary>
    [Authorize(Roles = "Users")]
    [HttpGet("GetPermissionMatrix")]
    public async Task<ActionResult<ApiSuccessResult<object>>> GetPermissionMatrixAsync([FromQuery] string? role, CancellationToken cancellationToken)
    {
        var modules = _actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(a => !Hidden.Contains(a.ControllerName))
            .Select(a => new { Module = a.ControllerName, Kind = PermissionActions.Kind(a.ActionName) })
            .Where(x => x.Kind != null)
            .GroupBy(x => x.Module)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var kinds = g.Select(x => x.Kind!).Distinct().ToList();
                var actions = new List<string>();
                if (kinds.Contains("Create")) actions.Add("Create");
                if (kinds.Contains("Update")) { actions.Add("Edit"); actions.Add("Confirm"); actions.Add("Cancel"); }
                if (kinds.Contains("Delete")) actions.Add("Delete");
                return new { module = g.Key, actions };
            }).ToList();

        var denies = string.IsNullOrEmpty(role) ? new List<PermissionRuleDto>() : await _sender.Send(new GetPermissionRulesRequest { RoleName = role }, cancellationToken);
        return Ok(new ApiSuccessResult<object>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPermissionMatrixAsync)}",
            Content = new { modules, denies = denies.Select(d => new { module = d.Module, actionKey = d.ActionKey }) }
        });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("SavePermissionRules")]
    public async Task<ActionResult<ApiSuccessResult<object>>> SavePermissionRulesAsync(SavePermissionRulesRequest request, CancellationToken cancellationToken)
    {
        if (string.Equals(request.RoleName, "Users", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status400BadRequest, new ApiErrorResult { Code = 400, Message = "The administrator role cannot be restricted." });

        var count = await _sender.Send(new SavePermissionRulesRequest
        {
            RoleName = request.RoleName, Denies = request.Denies, UpdatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
        }, cancellationToken);
        _cache.Remove(PermissionActions.CacheKey);
        return Ok(new ApiSuccessResult<object> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SavePermissionRulesAsync)}", Content = new { denied = count } });
    }
}
