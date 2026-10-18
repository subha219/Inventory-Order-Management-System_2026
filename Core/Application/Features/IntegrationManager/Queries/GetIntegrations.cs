using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.IntegrationManager.Queries;

public class ApiKeyDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? KeyPrefix { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class WebhookDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Secret { get; init; }
    public string? Events { get; init; }
    public bool IsActive { get; init; }
    public string? LastStatus { get; init; }
    public DateTime? LastAtUtc { get; init; }
}

public class TrackingShipmentDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? OrderNumber { get; init; }
    public string? Status { get; init; }
    public string? Carrier { get; init; }
    public DateTime? ShippedAtUtc { get; init; }
    public string? PublicToken { get; init; }
}

public class GetIntegrationsResult
{
    public List<ApiKeyDto> Keys { get; init; } = new();
    public List<WebhookDto> Webhooks { get; init; } = new();
    public List<TrackingShipmentDto> Shipments { get; init; } = new();
    public List<string> Events { get; init; } = new();
}

public class GetIntegrationsRequest : IRequest<GetIntegrationsResult>
{
}

public class GetIntegrationsHandler : IRequestHandler<GetIntegrationsRequest, GetIntegrationsResult>
{
    private readonly IQueryContext _context;

    public GetIntegrationsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetIntegrationsResult> Handle(GetIntegrationsRequest request, CancellationToken ct)
    {
        var keys = await _context.IntegrationApiKey.AsNoTracking().ApplyIsDeletedFilter(false).OrderByDescending(k => k.CreatedAtUtc).ToListAsync(ct);
        var hooks = await _context.WebhookEndpoint.AsNoTracking().ApplyIsDeletedFilter(false).OrderBy(h => h.Name).ToListAsync(ct);
        var ships = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).OrderByDescending(s => s.CreatedAtUtc).Take(30).ToListAsync(ct);
        var orderIds = ships.Select(s => s.DeliveryOrderId).Where(i => i != null).Distinct().ToList();
        var orders = await _context.DeliveryOrder.AsNoTracking().Where(o => orderIds.Contains(o.Id)).Select(o => new { o.Id, o.Number }).ToListAsync(ct);
        var orderNo = orders.ToDictionary(o => o.Id, o => o.Number);

        return new GetIntegrationsResult
        {
            Events = WebhookEvents.All.ToList(),
            Keys = keys.Select(k => new ApiKeyDto { Id = k.Id, Name = k.Name, KeyPrefix = k.KeyPrefix, IsActive = k.IsActive == true, LastUsedAtUtc = k.LastUsedAtUtc, CreatedAtUtc = k.CreatedAtUtc }).ToList(),
            Webhooks = hooks.Select(h => new WebhookDto { Id = h.Id, Name = h.Name, Url = h.Url, Secret = h.Secret, Events = h.Events, IsActive = h.IsActive == true, LastStatus = h.LastStatus, LastAtUtc = h.LastAtUtc }).ToList(),
            Shipments = ships.Select(s => new TrackingShipmentDto
            {
                Id = s.Id, Number = s.Number, Status = s.Status, Carrier = s.Carrier, ShippedAtUtc = s.ShippedAtUtc, PublicToken = s.PublicToken,
                OrderNumber = orderNo.TryGetValue(s.DeliveryOrderId ?? "", out var on) ? on : ""
            }).ToList()
        };
    }
}
