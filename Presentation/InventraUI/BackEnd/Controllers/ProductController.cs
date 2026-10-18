using Application.Features.ProductManager.Commands;
using Application.Features.ProductManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ProductController : BaseApiController
{
    public ProductController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("CreateProduct")]
    public async Task<ActionResult<ApiSuccessResult<CreateProductResult>>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateProductResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateProductAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdateProduct")]
    public async Task<ActionResult<ApiSuccessResult<UpdateProductResult>>> UpdateProductAsync(UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdateProductResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdateProductAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeleteProduct")]
    public async Task<ActionResult<ApiSuccessResult<DeleteProductResult>>> DeleteProductAsync(DeleteProductRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeleteProductResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeleteProductAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetProductList")]
    public async Task<ActionResult<ApiSuccessResult<GetProductListResult>>> GetProductListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetProductListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetProductListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetProductListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetDemandForecast")]
    public async Task<ActionResult<ApiSuccessResult<GetDemandForecastResult>>> GetDemandForecastAsync(
        CancellationToken cancellationToken,
        [FromQuery] int lookbackDays = 90,
        [FromQuery] int leadTimeDays = 7,
        [FromQuery] int serviceLevel = 95
        )
    {
        var request = new GetDemandForecastRequest { LookbackDays = lookbackDays, LeadTimeDays = leadTimeDays, ServiceLevel = serviceLevel };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetDemandForecastResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetDemandForecastAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("ApplyReorderRecommendations")]
    public async Task<ActionResult<ApiSuccessResult<ApplyReorderRecommendationsResult>>> ApplyReorderRecommendationsAsync(ApplyReorderRecommendationsRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<ApplyReorderRecommendationsResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ApplyReorderRecommendationsAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("AskInventory")]
    public async Task<ActionResult<ApiSuccessResult<AskInventoryResult>>> AskInventoryAsync(
        CancellationToken cancellationToken,
        [FromQuery] string question = ""
        )
    {
        var response = await _sender.Send(new AskInventoryRequest { Question = question }, cancellationToken);

        return Ok(new ApiSuccessResult<AskInventoryResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(AskInventoryAsync)}",
            Content = response
        });
    }
}
