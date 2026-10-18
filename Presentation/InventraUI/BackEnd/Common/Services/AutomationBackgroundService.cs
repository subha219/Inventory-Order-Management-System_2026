using Application.Features.AutomationManager.Commands;
using Application.Features.ScheduledReportManager.Commands;
using MediatR;
using Microsoft.Extensions.Options;

namespace InventraUI.BackEnd.Common.Services;

/// <summary>Runs the automation rules every 15 minutes so the bell and e-mails stay current without anyone opening a page.</summary>
public class AutomationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationBackgroundService> _logger;
    private readonly ReportSettings _reportSettings;

    public AutomationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AutomationBackgroundService> logger, IOptions<ReportSettings> reportSettings)
    {
        _reportSettings = reportSettings.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new RunAutomationRulesRequest(), stoppingToken);
                if (result.Problems.Count > 0) _logger.LogWarning("Automation rules: {Problems}", string.Join(" | ", result.Problems));

                var reports = await sender.Send(new RunScheduledReportsRequest { OffsetMinutes = _reportSettings.TimeZoneOffsetMinutes }, stoppingToken);
                if (reports.Sent > 0 || reports.Problems.Count > 0) _logger.LogInformation("Scheduled reports: {Message} {Problems}", reports.Message, string.Join(" | ", reports.Problems));
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automation rule check failed.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
