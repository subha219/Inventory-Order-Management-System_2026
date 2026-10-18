using Application.Features.PurchaseOrderManager.Commands;
using Application.Features.PurchaseOrderManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class PurchaseOrderController : BaseApiController
{
    public PurchaseOrderController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetReorderSuggestions")]
    public async Task<ActionResult<ApiSuccessResult<GetReorderSuggestionsResult>>> GetReorderSuggestionsAsync(CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetReorderSuggestionsRequest(), cancellationToken);

        return Ok(new ApiSuccessResult<GetReorderSuggestionsResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetReorderSuggestionsAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("CreateDraftFromReorder")]
    public async Task<ActionResult<ApiSuccessResult<CreateDraftPurchaseOrderFromReorderResult>>> CreateDraftFromReorderAsync(CreateDraftPurchaseOrderFromReorderRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateDraftPurchaseOrderFromReorderResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateDraftFromReorderAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("CreatePurchaseOrder")]
    public async Task<ActionResult<ApiSuccessResult<CreatePurchaseOrderResult>>> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreatePurchaseOrderResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreatePurchaseOrderAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdatePurchaseOrder")]
    public async Task<ActionResult<ApiSuccessResult<UpdatePurchaseOrderResult>>> UpdatePurchaseOrderAsync(UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdatePurchaseOrderResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdatePurchaseOrderAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeletePurchaseOrder")]
    public async Task<ActionResult<ApiSuccessResult<DeletePurchaseOrderResult>>> DeletePurchaseOrderAsync(DeletePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeletePurchaseOrderResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeletePurchaseOrderAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetPurchaseOrderList")]
    public async Task<ActionResult<ApiSuccessResult<GetPurchaseOrderListResult>>> GetPurchaseOrderListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetPurchaseOrderListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPurchaseOrderListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPurchaseOrderListAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetPurchaseOrderStatusList")]
    public async Task<ActionResult<ApiSuccessResult<GetPurchaseOrderStatusListResult>>> GetPurchaseOrderStatusListAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetPurchaseOrderStatusListRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPurchaseOrderStatusListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPurchaseOrderStatusListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetPurchaseOrderSingle")]
    public async Task<ActionResult<ApiSuccessResult<GetPurchaseOrderSingleResult>>> GetPurchaseOrderSingleAsync(
    CancellationToken cancellationToken,
    [FromQuery] string id
    )
    {
        var request = new GetPurchaseOrderSingleRequest { Id = id };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPurchaseOrderSingleResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPurchaseOrderSingleAsync)}",
            Content = response
        });
    }


}


