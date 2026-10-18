using System.Net;
using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Common.Services.EmailManager;
using Application.Features.ApprovalManager.Queries;
using Application.Features.AutomationManager.Queries;
using Application.Features.BackorderManager.Queries;
using Application.Features.ProductManager.Queries;
using Application.Features.PurchaseOrderManager.Queries;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AutomationManager.Commands;

public class RunAutomationRulesResult
{
    public int Checked { get; set; }
    public int Triggered { get; set; }
    public int EmailsSent { get; set; }
    public List<string> Problems { get; set; } = new();
}

public class RunAutomationRulesRequest : IRequest<RunAutomationRulesResult>
{
}

/// <summary>Checks every enabled rule, raises one in-app notification per rule per day, and e-mails once a day when recipients are set.</summary>
public class RunAutomationRulesHandler : IRequestHandler<RunAutomationRulesRequest, RunAutomationRulesResult>
{
    private readonly ISender _sender;
    private readonly IQueryContext _context;
    private readonly ICommandRepository<AutomationRule> _rules;
    private readonly ICommandRepository<AppNotification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;
    private readonly Application.Features.IntegrationManager.WebhookDispatcher _webhooks;

    public RunAutomationRulesHandler(ISender sender, IQueryContext context, ICommandRepository<AutomationRule> rules,
        ICommandRepository<AppNotification> notifications, IUnitOfWork unitOfWork, IEmailService email, Application.Features.IntegrationManager.WebhookDispatcher webhooks)
    {
        _webhooks = webhooks;
        _sender = sender;
        _context = context;
        _rules = rules;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _email = email;
    }


    public async Task<RunAutomationRulesResult> Handle(RunAutomationRulesRequest request, CancellationToken ct)
    {
        var result = new RunAutomationRulesResult();
        await _sender.Send(new GetAutomationRulesRequest(), ct); // makes sure the standard rules exist
        var rules = _rules.GetQuery().ApplyIsDeletedFilter().ToList();
        var today = DateTime.UtcNow.ToString("yyyyMMdd");

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            var def = AutomationCatalog.All.FirstOrDefault(d => d.Type == rule.RuleType);
            if (def == null) continue;
            result.Checked++;

            (int Count, string Title, List<string> Details) finding;
            try { finding = await Evaluate(rule, def, ct); }
            catch (Exception ex) { result.Problems.Add($"{def.Name}: {ex.Message}"); continue; }

            rule.LastCheckedUtc = DateTime.UtcNow;
            rule.LastCount = finding.Count;
            _rules.Update(rule);

            if (finding.Count <= 0) continue;
            result.Triggered++;
            var message = string.Join("; ", finding.Details.Take(5)) + (finding.Details.Count > 5 ? $"; and {finding.Details.Count - 5} more" : "");

            if (rule.NotifyInApp)
            {
                var existing = _notifications.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(n => n.RuleId == rule.Id && n.DayKey == today);
                if (existing == null)
                {
                    await _notifications.CreateAsync(new AppNotification
                    {
                        RuleId = rule.Id, RuleType = rule.RuleType, Severity = def.Severity, Title = finding.Title, Message = message,
                        Link = def.Link, ItemCount = finding.Count, DayKey = today
                    }, ct);
                    await _webhooks.DispatchAsync(Application.Features.IntegrationManager.WebhookEvents.AutomationAlert,
                        new { rule = def.Name, severity = def.Severity, title = finding.Title, count = finding.Count, details = finding.Details.Take(10).ToList() }, ct);
                }
                else
                {
                    existing.Title = finding.Title; existing.Message = message; existing.ItemCount = finding.Count;
                    _notifications.Update(existing);
                }
            }

            var recipients = (rule.EmailRecipients ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (recipients.Length > 0 && rule.LastEmailDay != today)
            {
                try
                {
                    var html = "<div style=\"font-family:Arial,sans-serif;font-size:14px\"><p><strong>" + WebUtility.HtmlEncode(finding.Title) + "</strong></p><ul>" +
                               string.Concat(finding.Details.Take(25).Select(d => "<li>" + WebUtility.HtmlEncode(d) + "</li>")) + "</ul>" +
                               (finding.Details.Count > 25 ? $"<p>...and {finding.Details.Count - 25} more.</p>" : "") +
                               $"<p>Open <a href=\"{def.Link}\">{def.Link}</a> in Inventra to act on it.</p></div>";
                    foreach (var to in recipients) await _email.SendEmailAsync(to, "[Inventra] " + finding.Title, html);
                    rule.LastEmailDay = today;
                    result.EmailsSent += recipients.Length;
                }
                catch (Exception ex)
                {
                    result.Problems.Add($"{def.Name}: e-mail failed ({ex.Message}). Check Users > Email Settings.");
                }
            }
        }

        await _unitOfWork.SaveAsync(ct);
        return result;
    }

    private async Task<(int Count, string Title, List<string> Details)> Evaluate(AutomationRule rule, RuleDefinition def, CancellationToken ct)
    {
        var threshold = rule.Threshold ?? def.DefaultThreshold;
        switch (rule.RuleType)
        {
            case "LowStock":
            {
                var items = (await _sender.Send(new GetReorderSuggestionsRequest(), ct)).Data ?? new List<ReorderSuggestionItem>();
                return (items.Count, $"{items.Count} product(s) need reordering",
                    items.Select(i => $"{i.ProductName} ({i.Stock:0.##} in stock, reorder at {i.ReorderPoint:0.##})").ToList());
            }
            case "ExpiringBatches":
            {
                var today = DateTime.UtcNow.Date;
                var limit = today.AddDays(threshold);
                var rows = await _context.ProductBatch.AsNoTracking().ApplyIsDeletedFilter(false)
                    .Where(b => (b.Quantity ?? 0) > 0 && b.ExpiryDate != null && b.ExpiryDate >= today && b.ExpiryDate <= limit)
                    .OrderBy(b => b.ExpiryDate).Take(200)
                    .Select(b => new { b.BatchNumber, ProductName = b.Product!.Name, b.ExpiryDate, Qty = b.Quantity ?? 0 }).ToListAsync(ct);
                return (rows.Count, $"{rows.Count} batch(es) expire within {threshold:0.##} days",
                    rows.Select(r => $"{r.ProductName} batch {r.BatchNumber}: {r.Qty:0.##} units, expires {r.ExpiryDate:dd MMM yyyy}").ToList());
            }
            case "ExpiredBatches":
            {
                var today = DateTime.UtcNow.Date;
                var rows = await _context.ProductBatch.AsNoTracking().ApplyIsDeletedFilter(false)
                    .Where(b => (b.Quantity ?? 0) > 0 && b.ExpiryDate != null && b.ExpiryDate < today)
                    .OrderBy(b => b.ExpiryDate).Take(200)
                    .Select(b => new { b.BatchNumber, ProductName = b.Product!.Name, b.ExpiryDate, Qty = b.Quantity ?? 0 }).ToListAsync(ct);
                return (rows.Count, $"{rows.Count} expired batch(es) still show stock",
                    rows.Select(r => $"{r.ProductName} batch {r.BatchNumber}: {r.Qty:0.##} units, expired {r.ExpiryDate:dd MMM yyyy}").ToList());
            }
            case "Overcommitted":
            {
                var res = await _sender.Send(new GetStockAvailabilityRequest(), ct);
                var rows = (res.Data ?? new List<StockAvailabilityDto>()).Where(r => r.State == "Overcommitted").ToList();
                return (rows.Count, $"{rows.Count} product(s) are promised beyond what is in stock",
                    rows.Select(r => $"{r.Name}: {r.OnHand:0.##} on hand, {r.Reserved:0.##} promised").ToList());
            }
            case "OldBackorders":
            {
                var res = await _sender.Send(new GetBackordersRequest(), ct);
                var rows = res.Lines.Where(l => l.Short > 0 && l.DaysWaiting >= threshold).ToList();
                return (rows.Count, $"{rows.Count} order line(s) have waited {threshold:0.##}+ days for stock",
                    rows.Select(r => $"{r.OrderNumber} ({r.Customer}): {r.ProductName} short {r.Short:0.##}, waiting {r.DaysWaiting}d").ToList());
            }
            case "PendingApprovals":
            {
                var res = await _sender.Send(new GetApprovalRequestListRequest { Status = "Pending" }, ct);
                var cutoff = DateTime.UtcNow.AddHours(-threshold);
                var rows = (res.Data ?? new List<ApprovalRequestDto>()).Where(r => r.RequestedAt == null || r.RequestedAt <= cutoff).ToList();
                return (rows.Count, $"{rows.Count} approval(s) are waiting for a decision",
                    rows.Select(r => $"{r.EntityNumber}: {r.Units:0.##} units").ToList());
            }
            default:
                return (0, "", new List<string>());
        }
    }
}
