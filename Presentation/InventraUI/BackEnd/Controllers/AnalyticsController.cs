using Application.Features.AnalyticsManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class AnalyticsController : BaseApiController
{
    public AnalyticsController(ISender sender) : base(sender)
    {
    }

    [Authorize(Roles = "StockReports")]
    [HttpGet("GetInventoryAnalysis")]
    public async Task<ActionResult<ApiSuccessResult<GetInventoryAnalysisResult>>> GetInventoryAnalysisAsync([FromQuery] GetInventoryAnalysisRequest request, CancellationToken ct)
    {
        var response = await _sender.Send(request, ct);
        return Ok(new ApiSuccessResult<GetInventoryAnalysisResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetInventoryAnalysisAsync)}", Content = response });
    }

    [Authorize(Roles = "PurchaseReports")]
    [HttpGet("GetSupplierScorecard")]
    public async Task<ActionResult<ApiSuccessResult<GetSupplierScorecardResult>>> GetSupplierScorecardAsync([FromQuery] GetSupplierScorecardRequest request, CancellationToken ct)
    {
        var response = await _sender.Send(request, ct);
        return Ok(new ApiSuccessResult<GetSupplierScorecardResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetSupplierScorecardAsync)}", Content = response });
    }

    [Authorize(Roles = "Warehouses")]
    [HttpGet("GetOperationsKpis")]
    public async Task<ActionResult<ApiSuccessResult<GetOperationsKpisResult>>> GetOperationsKpisAsync([FromQuery] GetOperationsKpisRequest request, CancellationToken ct)
    {
        var response = await _sender.Send(request, ct);
        return Ok(new ApiSuccessResult<GetOperationsKpisResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetOperationsKpisAsync)}", Content = response });
    }
}
