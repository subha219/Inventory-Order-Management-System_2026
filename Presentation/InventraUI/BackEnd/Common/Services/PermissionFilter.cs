using System.Security.Claims;
using Application.Features.PermissionManager;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace InventraUI.BackEnd.Common.Services;

/// <summary>What kind of action an API method is, for the permission rules.</summary>
public static class PermissionActions
{
    public static readonly string[] Keys = { "Create", "Edit", "Confirm", "Cancel", "Delete" };
    public const string CacheKey = "permission-rules";
    /// <summary>Holders of this role can look at everything their menu shows but the server refuses every change.</summary>
    public const string ReadOnlyRole = "Auditor / Read-only";

    /// <summary>Create, Delete or Update by the method name. Update is later split into Edit, Confirm or Cancel by the document status. Null means the method is not governed.</summary>
    public static string? Kind(string actionName)
    {
        if (actionName.StartsWith("Create", StringComparison.Ordinal)) return "Create";
        if (actionName.StartsWith("Delete", StringComparison.Ordinal)) return "Delete";
        if (actionName.StartsWith("Update", StringComparison.Ordinal)) return "Update";
        return null;
    }
}

/// <summary>
/// Runs before every API action. If a role the user holds has been denied this action in this module on the Permissions page,
/// the request is refused with a clear message. Administrators (role "Users") are never blocked, so nobody can lock themselves out.
/// </summary>
public class PermissionFilter : IAsyncActionFilter
{
    private readonly IMemoryCache _cache;
    private readonly ISender _sender;

    public PermissionFilter(IMemoryCache cache, ISender sender)
    {
        _cache = cache;
        _sender = sender;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || user.IsInRole("Users") || context.ActionDescriptor is not ControllerActionDescriptor cad)
        {
            await next();
            return;
        }

        // Read-only auditors: any request that is not a plain read is refused, except the person's own sign-in and profile calls.
        var method = context.HttpContext.Request.Method;
        if (user.IsInRole(PermissionActions.ReadOnlyRole)
            && !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method)
            && !string.Equals(cad.ControllerName, "Security", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new ApiErrorResult
            {
                Code = StatusCodes.Status403Forbidden,
                Message = "Your role is read-only. You can view everything your menu shows, but you cannot change anything."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        var kind = PermissionActions.Kind(cad.ActionName);
        if (kind == null) { await next(); return; }

        var key = kind;
        if (kind == "Update")
        {
            var status = context.ActionArguments.TryGetValue("request", out var arg) && arg != null ? arg.GetType().GetProperty("Status")?.GetValue(arg) as string : null;
            key = status == "2" ? "Confirm" : status == "1" ? "Cancel" : "Edit";
        }

        var module = cad.ControllerName;
        var rules = await _cache.GetOrCreateAsync(PermissionActions.CacheKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await _sender.Send(new GetPermissionRulesRequest(), context.HttpContext.RequestAborted);
        }) ?? new List<PermissionRuleDto>();

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var denied = rules.Any(r => r.Module == module && r.ActionKey == key && r.RoleName != null && roles.Contains(r.RoleName));
        if (!denied) { await next(); return; }

        context.Result = new ObjectResult(new ApiErrorResult
        {
            Code = StatusCodes.Status403Forbidden,
            Message = $"Your role is not allowed to {key.ToLowerInvariant()} in {module}. Ask an administrator to change this on the Permissions page."
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }
}
