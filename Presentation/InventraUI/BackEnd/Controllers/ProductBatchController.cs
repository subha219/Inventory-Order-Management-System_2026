using Application.Features.ProductBatchManager.Commands;
using Application.Features.ProductBatchManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ProductBatchController : BaseApiController
{
    public ProductBatchController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetProductBatchList")]
    public async Task<ActionResult<ApiSuccessResult<GetProductBatchListResult>>> GetProductBatchListAsync(
        CancellationToken cancellationToken,
        [FromQuery] int expiringDays = 30)
    {
        var response = await _sender.Send(new GetProductBatchListRequest { ExpiringDays = expiringDays }, cancellationToken);

        return Ok(new ApiSuccessResult<GetProductBatchListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetProductBatchListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("CreateProductBatch")]
    public async Task<ActionResult<ApiSuccessResult<CreateProductBatchResult>>> CreateProductBatchAsync(CreateProductBatchRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateProductBatchResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateProductBatchAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdateProductBatch")]
    public async Task<ActionResult<ApiSuccessResult<UpdateProductBatchResult>>> UpdateProductBatchAsync(UpdateProductBatchRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdateProductBatchResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdateProductBatchAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeleteProductBatch")]
    public async Task<ActionResult<ApiSuccessResult<DeleteProductBatchResult>>> DeleteProductBatchAsync(DeleteProductBatchRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeleteProductBatchResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeleteProductBatchAsync)}",
            Content = response
        });
    }
}
