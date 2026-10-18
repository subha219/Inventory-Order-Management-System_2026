using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.IntegrationManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.PortalManager.Commands;

public class PortalResult
{
    public string? Id { get; set; }
    public string? Token { get; set; }
    public string? Message { get; set; }
}

public class CreatePortalLinkRequest : IRequest<PortalResult>
{
    /// <summary>PurchaseOrder or SalesOrder.</summary>
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public int ExpiresInDays { get; init; } = 30;
    public string? UserId { get; init; }
}

public class CreatePortalLinkValidator : AbstractValidator<CreatePortalLinkRequest>
{
    public CreatePortalLinkValidator()
    {
        RuleFor(x => x.EntityType).Must(t => t == "PurchaseOrder" || t == "SalesOrder").WithMessage("Choose a purchase order or a sales order.");
        RuleFor(x => x.EntityId).NotEmpty().WithMessage("Choose the order.");
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 365);
    }
}

public class CreatePortalLinkHandler : IRequestHandler<CreatePortalLinkRequest, PortalResult>
{
    private readonly ICommandRepository<PortalLink> _links;
    private readonly ICommandRepository<PurchaseOrder> _pos;
    private readonly ICommandRepository<SalesOrder> _sos;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePortalLinkHandler(ICommandRepository<PortalLink> links, ICommandRepository<PurchaseOrder> pos, ICommandRepository<SalesOrder> sos, IUnitOfWork unitOfWork)
    {
        _links = links; _pos = pos; _sos = sos; _unitOfWork = unitOfWork;
    }

    public async Task<PortalResult> Handle(CreatePortalLinkRequest r, CancellationToken ct)
    {
        if (r.EntityType == "PurchaseOrder") _ = await _pos.GetAsync(r.EntityId!, ct) ?? throw new Exception("Purchase order not found.");
        else _ = await _sos.GetAsync(r.EntityId!, ct) ?? throw new Exception("Sales order not found.");

        // reuse a live link for the same order so a partner always has one address
        var now = DateTime.UtcNow;
        var existing = _links.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(l => l.EntityType == r.EntityType && l.EntityId == r.EntityId && l.IsActive == true && l.ExpiresAtUtc > now);
        if (existing != null) return new PortalResult { Id = existing.Id, Token = existing.Token, Message = "This order already has an active link." };

        var link = new PortalLink
        {
            EntityType = r.EntityType, EntityId = r.EntityId, Token = Guid.NewGuid().ToString("N"), IsActive = true,
            ExpiresAtUtc = now.AddDays(r.ExpiresInDays), CreatedById = r.UserId
        };
        await _links.CreateAsync(link, ct);
        await _unitOfWork.SaveAsync(ct);
        return new PortalResult { Id = link.Id, Token = link.Token, Message = "Link created." };
    }
}

public class DeactivatePortalLinkRequest : IRequest<PortalResult>
{
    public string? Id { get; init; }
    public string? UserId { get; init; }
}

public class DeactivatePortalLinkHandler : IRequestHandler<DeactivatePortalLinkRequest, PortalResult>
{
    private readonly ICommandRepository<PortalLink> _links;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivatePortalLinkHandler(ICommandRepository<PortalLink> links, IUnitOfWork unitOfWork)
    {
        _links = links; _unitOfWork = unitOfWork;
    }

    public async Task<PortalResult> Handle(DeactivatePortalLinkRequest r, CancellationToken ct)
    {
        var l = await _links.GetAsync(r.Id ?? string.Empty, ct) ?? throw new Exception("Link not found.");
        l.IsActive = false; l.UpdatedById = r.UserId;
        _links.Update(l);
        await _unitOfWork.SaveAsync(ct);
        return new PortalResult { Id = l.Id, Message = "Link switched off." };
    }
}

// ---------------------------------------------------------------- partner answers (no login)
public class SubmitPortalResponseRequest : IRequest<PortalResult>
{
    public string? Token { get; init; }
    public string? Status { get; init; }
    public DateTime? ExpectedDate { get; init; }
    public string? Message { get; init; }
}

public class SubmitPortalResponseValidator : AbstractValidator<SubmitPortalResponseRequest>
{
    public SubmitPortalResponseValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Status).Must(s => s == "Confirmed" || s == "Declined" || s == "Question").WithMessage("Choose confirm, decline or question.");
        RuleFor(x => x.Message).MaximumLength(1000);
    }
}

public class SubmitPortalResponseHandler : IRequestHandler<SubmitPortalResponseRequest, PortalResult>
{
    private readonly ICommandRepository<PortalLink> _links;
    private readonly ICommandRepository<PortalResponse> _responses;
    private readonly ICommandRepository<PurchaseOrder> _pos;
    private readonly ICommandRepository<SalesOrder> _sos;
    private readonly ICommandRepository<AppNotification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WebhookDispatcher _webhooks;

    public SubmitPortalResponseHandler(ICommandRepository<PortalLink> links, ICommandRepository<PortalResponse> responses, ICommandRepository<PurchaseOrder> pos,
        ICommandRepository<SalesOrder> sos, ICommandRepository<AppNotification> notifications, IUnitOfWork unitOfWork, WebhookDispatcher webhooks)
    {
        _links = links; _responses = responses; _pos = pos; _sos = sos; _notifications = notifications; _unitOfWork = unitOfWork; _webhooks = webhooks;
    }

    public async Task<PortalResult> Handle(SubmitPortalResponseRequest r, CancellationToken ct)
    {
        var token = r.Token!.Trim();
        var link = _links.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(l => l.Token == token);
        if (link == null || link.IsActive != true || (link.ExpiresAtUtc != null && link.ExpiresAtUtc < DateTime.UtcNow)) throw new Exception("This link is no longer active.");
        if (link.EntityType == "SalesOrder" && r.Status == "Declined") throw new Exception("Please use the question option to ask us about this order.");
        if (r.Status == "Question" && string.IsNullOrWhiteSpace(r.Message)) throw new Exception("Please type your question.");

        var linkId = link.Id;
        var already = _responses.GetQuery().ApplyIsDeletedFilter().Count(x => x.PortalLinkId == linkId);
        if (already >= 20) throw new Exception("Too many replies were sent on this link. Please contact us directly.");

        string? number, party;
        if (link.EntityType == "PurchaseOrder")
        {
            var po = _pos.GetQuery().Where(p => p.Id == link.EntityId).Select(p => new { p.Number, Vendor = p.Vendor!.Name }).FirstOrDefault();
            number = po?.Number; party = po?.Vendor;
        }
        else
        {
            var so = _sos.GetQuery().Where(p => p.Id == link.EntityId).Select(p => new { p.Number, Customer = p.Customer!.Name }).FirstOrDefault();
            number = so?.Number; party = so?.Customer;
        }

        var resp = new PortalResponse
        {
            PortalLinkId = link.Id, Status = r.Status, ExpectedDate = r.ExpectedDate, Message = string.IsNullOrWhiteSpace(r.Message) ? null : r.Message.Trim()
        };
        await _responses.CreateAsync(resp, ct);

        var title = $"{(link.EntityType == "PurchaseOrder" ? "Vendor" : "Customer")} {(r.Status == "Question" ? "asked a question about" : r.Status!.ToLowerInvariant() + "")} {number}";
        await _notifications.CreateAsync(new AppNotification
        {
            RuleType = "portal", Severity = r.Status == "Confirmed" ? "info" : "warning", Title = title,
            Message = (party ?? "") + (resp.ExpectedDate != null ? $" - expected {resp.ExpectedDate:yyyy-MM-dd}" : "") + (resp.Message != null ? " - " + (resp.Message.Length > 200 ? resp.Message[..200] : resp.Message) : ""),
            Link = "/PurchaseOrders/Portal", ItemCount = 1, DayKey = DateTime.UtcNow.ToString("yyyyMMdd")
        }, ct);
        await _unitOfWork.SaveAsync(ct);

        await _webhooks.DispatchAsync(WebhookEvents.PortalResponse, new
        {
            orderType = link.EntityType, orderNumber = number, party, status = r.Status, expectedDate = r.ExpectedDate, message = resp.Message
        }, ct);
        return new PortalResult { Id = resp.Id, Message = "Thank you. Your reply has been sent." };
    }
}
