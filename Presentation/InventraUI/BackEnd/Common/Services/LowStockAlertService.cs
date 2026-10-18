using System.Net;
using System.Text;
using Application.Common.Services.EmailManager;
using Application.Features.PurchaseOrderManager.Queries;
using Infrastructure.EmailManager;
using MediatR;
using Microsoft.Extensions.Options;

namespace InventraUI.BackEnd.Common.Services;

public class LowStockAlertSettings
{
    /// <summary>Turn the daily e-mail on. Needs SmtpSettings and at least one recipient.</summary>
    public bool Enabled { get; set; }
    public string[] Recipients { get; set; } = Array.Empty<string>();
    /// <summary>The alert goes out once a day, the first time the check runs at or after this UTC hour (3 = about 08:30 India time).</summary>
    public int SendAtHourUtc { get; set; } = 3;
}

public class LowStockAlertResult
{
    public bool Sent { get; init; }
    public int ItemCount { get; init; }
    public int RecipientCount { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>Builds and e-mails the low-stock digest. Used by the daily background job and by the "email now" button.</summary>
public class LowStockAlertSender
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LowStockAlertSettings _settings;
    private readonly SmtpSettingsStore _smtpStore;
    private readonly ILogger<LowStockAlertSender> _logger;

    public LowStockAlertSender(
        IServiceScopeFactory scopeFactory,
        IOptions<LowStockAlertSettings> settings,
        SmtpSettingsStore smtpStore,
        ILogger<LowStockAlertSender> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _smtpStore = smtpStore;
        _logger = logger;
    }

    public async Task<LowStockAlertResult> SendAsync(CancellationToken ct)
    {
        var recipients = _settings.Recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
        if (recipients.Count == 0)
            return new LowStockAlertResult { Message = "No recipients set. Add addresses under LowStockAlerts:Recipients in appsettings." };
        if (string.IsNullOrWhiteSpace(_smtpStore.Resolve().Host))
            return new LowStockAlertResult { Message = "E-mail is not set up yet. An administrator can fill it in under Users > Email Settings." };

        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var result = await sender.Send(new GetReorderSuggestionsRequest(), ct);
        var items = result.Data ?? new List<ReorderSuggestionItem>();
        if (items.Count == 0)
            return new LowStockAlertResult { Message = "Nothing is at or below its reorder point, so no email was sent." };

        var subject = $"[Inventra] {items.Count} product(s) need reordering";
        var html = BuildHtml(items);
        foreach (var to in recipients)
            await email.SendEmailAsync(to, subject, html);

        _logger.LogInformation("Low-stock alert sent to {Count} recipient(s) for {Items} product(s).", recipients.Count, items.Count);
        return new LowStockAlertResult
        {
            Sent = true,
            ItemCount = items.Count,
            RecipientCount = recipients.Count,
            Message = $"Alert for {items.Count} product(s) sent to {recipients.Count} recipient(s). If it does not arrive, check the app log for SMTP errors."
        };
    }

    private static string BuildHtml(List<ReorderSuggestionItem> items)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:14px\">");
        sb.Append($"<p><strong>{items.Count}</strong> product(s) are at or below their reorder point.</p>");
        sb.Append("<table cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;border:1px solid #ddd\">");
        sb.Append("<tr style=\"background:#f3f3f3\"><th align=\"left\">Product</th><th align=\"right\">In stock</th><th align=\"right\">Reorder point</th><th align=\"right\">Suggested order</th></tr>");
        foreach (var i in items.Take(50))
        {
            sb.Append("<tr style=\"border-top:1px solid #ddd\">");
            sb.Append($"<td>{WebUtility.HtmlEncode(i.ProductName ?? "")} <small style=\"color:#888\">{WebUtility.HtmlEncode(i.ProductNumber ?? "")}</small></td>");
            sb.Append($"<td align=\"right\">{i.Stock:0.##}</td><td align=\"right\">{i.ReorderPoint:0.##}</td><td align=\"right\">{i.SuggestedQuantity:0.##}</td></tr>");
        }
        sb.Append("</table>");
        if (items.Count > 50) sb.Append($"<p>...and {items.Count - 50} more.</p>");
        sb.Append("<p>Open Purchase &rarr; Reorder Suggestions in Inventra to create a draft purchase order.</p></div>");
        return sb.ToString();
    }
}

/// <summary>Checks every 15 minutes and sends the digest once per day when enabled.</summary>
public class LowStockAlertBackgroundService : BackgroundService
{
    private readonly LowStockAlertSender _alertSender;
    private readonly LowStockAlertSettings _settings;
    private readonly ILogger<LowStockAlertBackgroundService> _logger;
    private DateTime _lastRunDate = DateTime.MinValue;

    public LowStockAlertBackgroundService(
        LowStockAlertSender alertSender,
        IOptions<LowStockAlertSettings> settings,
        ILogger<LowStockAlertBackgroundService> logger)
    {
        _alertSender = alertSender;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled) return;

        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now.Hour >= _settings.SendAtHourUtc && _lastRunDate != now.Date)
                {
                    _lastRunDate = now.Date;
                    var result = await _alertSender.SendAsync(stoppingToken);
                    _logger.LogInformation("Low-stock alert job: {Message}", result.Message);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Low-stock alert job failed.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
