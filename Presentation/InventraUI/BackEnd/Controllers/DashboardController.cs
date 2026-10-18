using Application.Features.DashboardManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class DashboardController : BaseApiController
{
    public DashboardController(ISender sender) : base(sender)
    {
    }


    /// <summary>To-do counts for the signed-in person, limited to what their roles allow.</summary>
    [Authorize]
    [HttpGet("GetMyWork")]
    public async Task<ActionResult<ApiSuccessResult<GetMyWorkResult>>> GetMyWorkAsync(CancellationToken cancellationToken)
    {
        var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        var response = await _sender.Send(new GetMyWorkRequest { Roles = roles, UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) }, cancellationToken);

        return Ok(new ApiSuccessResult<GetMyWorkResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetMyWorkAsync)}",
            Content = response
        });
    }

    /// <summary>The signed-in person's own dashboard data: headline numbers, trends and lists for their job.</summary>
    [Authorize]
    [HttpGet("GetRoleDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetRoleDashboardResult>>> GetRoleDashboardAsync(CancellationToken cancellationToken)
    {
        var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        var response = await _sender.Send(new GetRoleDashboardRequest { Roles = roles, UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) }, cancellationToken);

        return Ok(new ApiSuccessResult<GetRoleDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetRoleDashboardAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetCardsDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetCardsDashboardResult>>> GetCardsDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetCardsDashboardRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetCardsDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetCardsDashboardAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetSalesDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetSalesDashboardResult>>> GetSalesDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetSalesDashboardRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSalesDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSalesDashboardAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetPurchaseDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetPurchaseDashboardResult>>> GetPurchaseDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetPurchaseDashboardRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPurchaseDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPurchaseDashboardAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetInventoryDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetInventoryDashboardResult>>> GetInventoryDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetInventoryDashboardRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetInventoryDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetInventoryDashboardAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetInsightsDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetInsightsDashboardResult>>> GetInsightsDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetInsightsDashboardRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetInsightsDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetInsightsDashboardAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetAnalyticsDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetAnalyticsDashboardResult>>> GetAnalyticsDashboardAsync(
        CancellationToken cancellationToken,
        [FromQuery] int days = 30)
    {
        var response = await _sender.Send(new GetAnalyticsDashboardRequest { Days = days }, cancellationToken);

        return Ok(new ApiSuccessResult<GetAnalyticsDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetAnalyticsDashboardAsync)}",
            Content = response
        });
    }

}
