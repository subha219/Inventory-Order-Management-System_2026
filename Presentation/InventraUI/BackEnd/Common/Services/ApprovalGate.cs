using System.Security.Claims;
using Application.Features.ApprovalManager.Commands;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace InventraUI.BackEnd.Common.Services;

public class ApprovalSettings
{
    /// <summary>Turn the approval rule on.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Adjustments and scrapping that move this many units or more need an approver before they can be confirmed.</summary>
    public double UnitsThreshold { get; set; } = 100;
    /// <summary>People in any of these roles can confirm directly and can approve or reject requests.</summary>
    public string[] ApproverRoles { get; set; } = new[] { "Users" };
}

/// <summary>
/// Put on the Update action of a stock adjustment or scrapping controller. When someone who is not an approver tries
/// to set the status to Confirmed and the document moves at least the threshold number of units, nothing is saved;
/// a pending approval request is recorded instead and the caller gets a clear message.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RequireApprovalAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _entityType;

    public RequireApprovalAttribute(string entityType)
    {
        _entityType = entityType;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<ApprovalSettings>>().Value;
        var user = context.HttpContext.User;

        if (!settings.Enabled || settings.ApproverRoles.Any(user.IsInRole) ||
            !context.ActionArguments.TryGetValue("request", out var arg) || arg == null)
        {
            await next();
            return;
        }

        var id = arg.GetType().GetProperty("Id")?.GetValue(arg) as string;
        var status = arg.GetType().GetProperty("Status")?.GetValue(arg) as string;
        if (string.IsNullOrEmpty(id) || status != "2")
        {
            await next();
            return;
        }

        var sender = context.HttpContext.RequestServices.GetRequiredService<ISender>();
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await sender.Send(new EvaluateApprovalRequest
        {
            EntityType = _entityType,
            EntityId = id,
            RequestedById = userId,
            UnitsThreshold = settings.UnitsThreshold
        }, context.HttpContext.RequestAborted);

        if (!result.RequiresApproval)
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new ApiErrorResult
        {
            Code = StatusCodes.Status409Conflict,
            Message = $"{result.Number} moves {result.Units:0.##} units, which needs an approver. An approval request was sent. Ask an approver to open Approvals and approve it; the document will then be confirmed."
        })
        { StatusCode = StatusCodes.Status409Conflict };
    }
}
