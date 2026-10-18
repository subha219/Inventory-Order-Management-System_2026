using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.IntegrationManager.Commands;

public class IntegrationResult
{
    public string? Id { get; set; }
    public string? Message { get; set; }
    /// <summary>A secret shown once (a new API key) or the tracking token.</summary>
    public string? Secret { get; set; }
}

// ---------------------------------------------------------------- api keys
public class CreateApiKeyRequest : IRequest<IntegrationResult>
{
    public string? Name { get; init; }
    public string? UserId { get; init; }
}

public class CreateApiKeyValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give the key a name, for example the system that will use it.").MaximumLength(100);
    }
}

public class CreateApiKeyHandler : IRequestHandler<CreateApiKeyRequest, IntegrationResult>
{
    private readonly ICommandRepository<IntegrationApiKey> _keys;
    private readonly IUnitOfWork _unitOfWork;

    public CreateApiKeyHandler(ICommandRepository<IntegrationApiKey> keys, IUnitOfWork unitOfWork)
    {
        _keys = keys; _unitOfWork = unitOfWork;
    }

    public async Task<IntegrationResult> Handle(CreateApiKeyRequest r, CancellationToken ct)
    {
        var raw = "whms_" + ApiKeyService.NewRandomHex(24);
        var k = new IntegrationApiKey { Name = r.Name!.Trim(), KeyPrefix = raw[..9], KeyHash = ApiKeyService.Hash(raw), IsActive = true, CreatedById = r.UserId };
        await _keys.CreateAsync(k, ct);
        await _unitOfWork.SaveAsync(ct);
        return new IntegrationResult { Id = k.Id, Secret = raw, Message = "Key created. Copy it now, it cannot be shown again." };
    }
}

public class RevokeApiKeyRequest : IRequest<IntegrationResult>
{
    public string? Id { get; init; }
    public string? UserId { get; init; }
}

public class RevokeApiKeyHandler : IRequestHandler<RevokeApiKeyRequest, IntegrationResult>
{
    private readonly ICommandRepository<IntegrationApiKey> _keys;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeApiKeyHandler(ICommandRepository<IntegrationApiKey> keys, IUnitOfWork unitOfWork)
    {
        _keys = keys; _unitOfWork = unitOfWork;
    }

    public async Task<IntegrationResult> Handle(RevokeApiKeyRequest r, CancellationToken ct)
    {
        var k = await _keys.GetAsync(r.Id ?? string.Empty, ct) ?? throw new Exception("Key not found.");
        k.UpdatedById = r.UserId;
        _keys.Delete(k);
        await _unitOfWork.SaveAsync(ct);
        return new IntegrationResult { Id = k.Id, Message = "Key revoked." };
    }
}

// ---------------------------------------------------------------- webhooks
public class SaveWebhookRequest : IRequest<IntegrationResult>
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Events { get; init; }
    public bool IsActive { get; init; } = true;
    public string? UserId { get; init; }
}

public class SaveWebhookValidator : AbstractValidator<SaveWebhookRequest>
{
    public SaveWebhookValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(500)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .WithMessage("Enter a full web address starting with https://");
        RuleFor(x => x.Events).NotEmpty().WithMessage("Choose at least one event.");
    }
}

public class SaveWebhookHandler : IRequestHandler<SaveWebhookRequest, IntegrationResult>
{
    private readonly ICommandRepository<WebhookEndpoint> _hooks;
    private readonly IUnitOfWork _unitOfWork;

    public SaveWebhookHandler(ICommandRepository<WebhookEndpoint> hooks, IUnitOfWork unitOfWork)
    {
        _hooks = hooks; _unitOfWork = unitOfWork;
    }

    public async Task<IntegrationResult> Handle(SaveWebhookRequest r, CancellationToken ct)
    {
        WebhookEndpoint? h = string.IsNullOrEmpty(r.Id) ? null : await _hooks.GetAsync(r.Id, ct);
        var isNew = h == null;
        if (h == null)
        {
            h = new WebhookEndpoint { Secret = "whsec_" + ApiKeyService.NewRandomHex(20), CreatedById = r.UserId };
            _hooks.Create(h);
        }
        else h.UpdatedById = r.UserId;
        h.Name = r.Name!.Trim(); h.Url = r.Url!.Trim(); h.Events = r.Events; h.IsActive = r.IsActive;
        if (!isNew) _hooks.Update(h);
        await _unitOfWork.SaveAsync(ct);
        return new IntegrationResult { Id = h.Id, Message = "Webhook saved." };
    }
}

public class DeleteWebhookRequest : IRequest<IntegrationResult>
{
    public string? Id { get; init; }
    public string? UserId { get; init; }
}

public class DeleteWebhookHandler : IRequestHandler<DeleteWebhookRequest, IntegrationResult>
{
    private readonly ICommandRepository<WebhookEndpoint> _hooks;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteWebhookHandler(ICommandRepository<WebhookEndpoint> hooks, IUnitOfWork unitOfWork)
    {
        _hooks = hooks; _unitOfWork = unitOfWork;
    }

    public async Task<IntegrationResult> Handle(DeleteWebhookRequest r, CancellationToken ct)
    {
        var h = await _hooks.GetAsync(r.Id ?? string.Empty, ct) ?? throw new Exception("Webhook not found.");
        h.UpdatedById = r.UserId;
        _hooks.Delete(h);
        await _unitOfWork.SaveAsync(ct);
        return new IntegrationResult { Id = h.Id, Message = "Webhook removed." };
    }
}

public class TestWebhookRequest : IRequest<IntegrationResult>
{
    public string? Id { get; init; }
}

public class TestWebhookHandler : IRequestHandler<TestWebhookRequest, IntegrationResult>
{
    private readonly ICommandRepository<WebhookEndpoint> _hooks;
    private readonly IUnitOfWork _unitOfWork;

    public TestWebhookHandler(ICommandRepository<WebhookEndpoint> hooks, IUnitOfWork unitOfWork)
    {
        _hooks = hooks; _unitOfWork = unitOfWork;
    }

    public async Task<IntegrationResult> Handle(TestWebhookRequest r, CancellationToken ct)
    {
        var h = await _hooks.GetAsync(r.Id ?? string.Empty, ct) ?? throw new Exception("Webhook not found.");
        var body = WebhookDispatcher.BuildBody(WebhookEvents.Test, new { message = "This is a test from the warehouse system." });
        var status = await WebhookDispatcher.PostAsync(h, WebhookEvents.Test, body, ct);
        h.LastStatus = status; h.LastAtUtc = DateTime.UtcNow;
        _hooks.Update(h);
        await _unitOfWork.SaveAsync(ct);
        return new IntegrationResult { Id = h.Id, Message = "Test sent. Response: " + status };
    }
}

// ---------------------------------------------------------------- tracking links
public class CreateTrackingLinkRequest : IRequest<IntegrationResult>
{
    public string? ShipmentId { get; init; }
    public string? UserId { get; init; }
}

public class CreateTrackingLinkHandler : IRequestHandler<CreateTrackingLinkRequest, IntegrationResult>
{
    private readonly ICommandRepository<Shipment> _shipments;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTrackingLinkHandler(ICommandRepository<Shipment> shipments, IUnitOfWork unitOfWork)
    {
        _shipments = shipments; _unitOfWork = unitOfWork;
    }

    public async Task<IntegrationResult> Handle(CreateTrackingLinkRequest r, CancellationToken ct)
    {
        var s = await _shipments.GetAsync(r.ShipmentId ?? string.Empty, ct) ?? throw new Exception("Shipment not found.");
        if (string.IsNullOrEmpty(s.PublicToken))
        {
            s.PublicToken = Guid.NewGuid().ToString("N");
            s.UpdatedById = r.UserId;
            _shipments.Update(s);
            await _unitOfWork.SaveAsync(ct);
        }
        return new IntegrationResult { Id = s.Id, Secret = s.PublicToken, Message = "Tracking link ready." };
    }
}
