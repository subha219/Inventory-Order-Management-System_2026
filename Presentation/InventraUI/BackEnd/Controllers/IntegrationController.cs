using Application.Features.IntegrationManager;
using Application.Features.IntegrationManager.Commands;
using Application.Features.IntegrationManager.Queries;
using InventraUI.BackEnd.Common.Base;
using InventraUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventraUI.BackEnd.Controllers;

/// <summary>Admin screens for API keys, webhooks and tracking links.</summary>
[Route("api/[controller]")]
public class IntegrationController : BaseApiController
{
    public IntegrationController(ISender sender) : base(sender)
    {
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Users")]
    [HttpGet("GetIntegrations")]
    public async Task<ActionResult<ApiSuccessResult<GetIntegrationsResult>>> GetIntegrationsAsync(CancellationToken ct)
    {
        var response = await _sender.Send(new GetIntegrationsRequest(), ct);
        return Ok(new ApiSuccessResult<GetIntegrationsResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetIntegrationsAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("CreateApiKey")]
    public async Task<ActionResult<ApiSuccessResult<IntegrationResult>>> CreateApiKeyAsync(CreateApiKeyRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new CreateApiKeyRequest { Name = r.Name, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<IntegrationResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(CreateApiKeyAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("RevokeApiKey")]
    public async Task<ActionResult<ApiSuccessResult<IntegrationResult>>> RevokeApiKeyAsync(RevokeApiKeyRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new RevokeApiKeyRequest { Id = r.Id, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<IntegrationResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(RevokeApiKeyAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("SaveWebhook")]
    public async Task<ActionResult<ApiSuccessResult<IntegrationResult>>> SaveWebhookAsync(SaveWebhookRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new SaveWebhookRequest { Id = r.Id, Name = r.Name, Url = r.Url, Events = r.Events, IsActive = r.IsActive, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<IntegrationResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(SaveWebhookAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("DeleteWebhook")]
    public async Task<ActionResult<ApiSuccessResult<IntegrationResult>>> DeleteWebhookAsync(DeleteWebhookRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new DeleteWebhookRequest { Id = r.Id, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<IntegrationResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(DeleteWebhookAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("TestWebhook")]
    public async Task<ActionResult<ApiSuccessResult<IntegrationResult>>> TestWebhookAsync(TestWebhookRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new TestWebhookRequest { Id = r.Id }, ct);
        return Ok(new ApiSuccessResult<IntegrationResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(TestWebhookAsync)}", Content = response });
    }

    [Authorize(Roles = "Users")]
    [HttpPost("CreateTrackingLink")]
    public async Task<ActionResult<ApiSuccessResult<IntegrationResult>>> CreateTrackingLinkAsync(CreateTrackingLinkRequest r, CancellationToken ct)
    {
        var response = await _sender.Send(new CreateTrackingLinkRequest { ShipmentId = r.ShipmentId, UserId = UserId }, ct);
        return Ok(new ApiSuccessResult<IntegrationResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(CreateTrackingLinkAsync)}", Content = response });
    }
}

/// <summary>Public, no-login endpoint used by the customer tracking page. The secret link token is the only key.</summary>
[Route("api/public")]
public class PublicController : BaseApiController
{
    public PublicController(ISender sender) : base(sender)
    {
    }

    [AllowAnonymous]
    [HttpGet("tracking/{token}")]
    public async Task<ActionResult<ApiSuccessResult<TrackingResult>>> TrackingAsync(string token, CancellationToken ct)
    {
        var response = await _sender.Send(new GetPublicTrackingRequest { Token = token }, ct);
        if (!response.Found) return NotFound(new ApiSuccessResult<TrackingResult> { Code = StatusCodes.Status404NotFound, Message = "Tracking link not found.", Content = response });
        return Ok(new ApiSuccessResult<TrackingResult> { Code = StatusCodes.Status200OK, Message = "OK", Content = response });
    }
}

/// <summary>Read-only API for other systems. Send the key in the X-Api-Key header.</summary>
[Route("api/integration")]
public class IntegrationApiController : BaseApiController
{
    private readonly ApiKeyService _keys;

    public IntegrationApiController(ISender sender, ApiKeyService keys) : base(sender)
    {
        _keys = keys;
    }

    private Task<bool> AuthorizedAsync(CancellationToken ct) => _keys.ValidateAsync(Request.Headers["X-Api-Key"].FirstOrDefault(), ct);

    [AllowAnonymous]
    [HttpGet("stock")]
    public async Task<IActionResult> StockAsync([FromQuery] string? sku, [FromQuery] string? warehouse, CancellationToken ct)
    {
        if (!await AuthorizedAsync(ct)) return Unauthorized(new { message = "Missing or invalid X-Api-Key header." });
        var rows = await _sender.Send(new GetStockLevelsRequest { Sku = sku, Warehouse = warehouse }, ct);
        return Ok(new { count = rows.Count, items = rows });
    }

    [AllowAnonymous]
    [HttpGet("shipments/{number}")]
    public async Task<IActionResult> ShipmentAsync(string number, CancellationToken ct)
    {
        if (!await AuthorizedAsync(ct)) return Unauthorized(new { message = "Missing or invalid X-Api-Key header." });
        var res = await _sender.Send(new GetShipmentByNumberRequest { Number = number }, ct);
        if (!res.Found) return NotFound(new { message = "Shipment not found." });
        return Ok(res);
    }
}
