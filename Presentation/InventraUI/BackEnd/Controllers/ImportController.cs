using Application.Features.ImportManager.Commands;
using Application.Features.StockReportManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ImportController : BaseApiController
{
    public ImportController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Products")]
    [HttpPost("ImportProducts")]
    public async Task<ActionResult<ApiSuccessResult<ImportResult>>> ImportProductsAsync(ImportProductsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ImportProductsRequest { Rows = request.Rows, DryRun = request.DryRun, CreatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<ImportResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ImportProductsAsync)}", Content = response });
    }

    [Authorize(Roles = "Products")]
    [HttpPost("ImportOpeningStock")]
    public async Task<ActionResult<ApiSuccessResult<ImportResult>>> ImportOpeningStockAsync(ImportOpeningStockRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ImportOpeningStockRequest
        {
            WarehouseId = request.WarehouseId, Date = request.Date, Description = request.Description, Rows = request.Rows, DryRun = request.DryRun, CreatedById = UserId
        }, cancellationToken);
        return Ok(new ApiSuccessResult<ImportResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ImportOpeningStockAsync)}", Content = response });
    }

    [Authorize(Roles = "Products")]
    [HttpPost("ImportVendors")]
    public async Task<ActionResult<ApiSuccessResult<ImportResult>>> ImportVendorsAsync(ImportVendorsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ImportVendorsRequest { Rows = request.Rows, DryRun = request.DryRun, CreatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<ImportResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ImportVendorsAsync)}", Content = response });
    }

    [Authorize(Roles = "Products")]
    [HttpPost("ImportCustomers")]
    public async Task<ActionResult<ApiSuccessResult<ImportResult>>> ImportCustomersAsync(ImportCustomersRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ImportCustomersRequest { Rows = request.Rows, DryRun = request.DryRun, CreatedById = UserId }, cancellationToken);
        return Ok(new ApiSuccessResult<ImportResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(ImportCustomersAsync)}", Content = response });
    }

    [Authorize]
    [HttpGet("GetInventoryInsights")]
    public async Task<ActionResult<ApiSuccessResult<GetInventoryInsightsResult>>> GetInventoryInsightsAsync([FromQuery] GetInventoryInsightsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetInventoryInsightsResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetInventoryInsightsAsync)}", Content = response });
    }
}
