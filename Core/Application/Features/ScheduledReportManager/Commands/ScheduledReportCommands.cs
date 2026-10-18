using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Common.Services.EmailManager;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Application.Features.ScheduledReportManager.Commands;

public class ScheduledReportDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? ReportType { get; init; }
    public string? Frequency { get; init; }
    public int? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public int SendHour { get; init; }
    public int SendMinute { get; init; }
    public string? Recipients { get; init; }
    public bool Enabled { get; init; }
    public string? WarehouseId { get; init; }
    public int? SlowDays { get; init; }
    public DateTime? LastSent { get; init; }
    public string? LastStatus { get; init; }
    public DateTime? NextRun { get; init; }
}

public class GetScheduledReportsResult
{
    public List<ScheduledReportDto> Data { get; init; } = new();
    public List<ReportTypeInfo> Types { get; init; } = new();
}

public class GetScheduledReportsRequest : IRequest<GetScheduledReportsResult>
{
    public int OffsetMinutes { get; init; }
}

internal static class Schedule
{
    /// <summary>The most recent moment (in UTC) this schedule should have fired, looking back from now.</summary>
    public static DateTime LastOccurrenceUtc(ScheduledReport r, DateTime nowUtc, int offsetMinutes)
    {
        var local = nowUtc.AddMinutes(offsetMinutes);
        DateTime At(DateTime d) => d.Date.AddHours(r.SendHour).AddMinutes(r.SendMinute);
        DateTime occ;
        switch (r.Frequency)
        {
            case "Weekly":
            {
                var target = r.DayOfWeek ?? 1;
                var back = ((int)local.DayOfWeek - target + 7) % 7;
                occ = At(local.AddDays(-back));
                if (occ > local) occ = occ.AddDays(-7);
                break;
            }
            case "Monthly":
            {
                DateTime InMonth(DateTime m) => At(new DateTime(m.Year, m.Month, Math.Min(r.DayOfMonth ?? 1, DateTime.DaysInMonth(m.Year, m.Month))));
                occ = InMonth(local);
                if (occ > local) occ = InMonth(local.AddMonths(-1));
                break;
            }
            default:
                occ = At(local);
                if (occ > local) occ = occ.AddDays(-1);
                break;
        }
        return occ.AddMinutes(-offsetMinutes);
    }

    public static DateTime NextOccurrenceUtc(ScheduledReport r, DateTime nowUtc, int offsetMinutes)
    {
        var last = LastOccurrenceUtc(r, nowUtc, offsetMinutes);
        var step = r.Frequency == "Weekly" ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);
        if (r.Frequency == "Monthly")
        {
            // walk forward day by day from the last firing until the schedule would fire again
            var probe = last.AddHours(1);
            for (var i = 0; i < 40; i++)
            {
                if (LastOccurrenceUtc(r, probe, offsetMinutes) > last) return LastOccurrenceUtc(r, probe, offsetMinutes);
                probe = probe.AddDays(1);
            }
            return last.AddDays(30);
        }
        return last + step;
    }
}

public class GetScheduledReportsHandler : IRequestHandler<GetScheduledReportsRequest, GetScheduledReportsResult>
{
    private readonly IQueryContext _context;

    public GetScheduledReportsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetScheduledReportsResult> Handle(GetScheduledReportsRequest request, CancellationToken ct)
    {
        var rows = await _context.ScheduledReport.AsNoTracking().ApplyIsDeletedFilter(false).OrderBy(x => x.Name).ToListAsync(ct);
        var now = DateTime.UtcNow;
        return new GetScheduledReportsResult
        {
            Types = ReportCatalog.All,
            Data = rows.Select(r => new ScheduledReportDto
            {
                Id = r.Id, Name = r.Name, ReportType = r.ReportType, Frequency = r.Frequency, DayOfWeek = r.DayOfWeek, DayOfMonth = r.DayOfMonth,
                SendHour = r.SendHour, SendMinute = r.SendMinute, Recipients = r.Recipients, Enabled = r.Enabled, WarehouseId = r.WarehouseId,
                SlowDays = r.SlowDays, LastSent = r.LastSentUtc, LastStatus = r.LastStatus,
                NextRun = r.Enabled ? Schedule.NextOccurrenceUtc(r, now, request.OffsetMinutes) : null
            }).ToList()
        };
    }
}

// ---------------------------------------------------------------- save / delete
public class SaveScheduledReportRequest : IRequest<string>
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? ReportType { get; init; }
    public string? Frequency { get; init; }
    public int? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public int SendHour { get; init; }
    public int SendMinute { get; init; }
    public string? Recipients { get; init; }
    public bool Enabled { get; init; }
    public string? WarehouseId { get; init; }
    public int? SlowDays { get; init; }
    public string? UpdatedById { get; init; }
}

public class SaveScheduledReportValidator : AbstractValidator<SaveScheduledReportRequest>
{
    public SaveScheduledReportValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ReportType).Must(t => ReportCatalog.All.Any(c => c.Type == t)).WithMessage("Choose a report.");
        RuleFor(x => x.Frequency).Must(f => f == "Daily" || f == "Weekly" || f == "Monthly").WithMessage("Choose how often to send.");
        RuleFor(x => x.SendHour).InclusiveBetween(0, 23);
        RuleFor(x => x.SendMinute).InclusiveBetween(0, 59);
        RuleFor(x => x.DayOfWeek).InclusiveBetween(0, 6).When(x => x.Frequency == "Weekly");
        RuleFor(x => x.DayOfMonth).InclusiveBetween(1, 31).When(x => x.Frequency == "Monthly");
    }
}

public class SaveScheduledReportHandler : IRequestHandler<SaveScheduledReportRequest, string>
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private readonly ICommandRepository<ScheduledReport> _reports;
    private readonly IUnitOfWork _unitOfWork;

    public SaveScheduledReportHandler(ICommandRepository<ScheduledReport> reports, IUnitOfWork unitOfWork)
    {
        _reports = reports;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> Handle(SaveScheduledReportRequest request, CancellationToken ct)
    {
        var emails = (request.Recipients ?? string.Empty).Split(new[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (emails.Count == 0) throw new Exception("Add at least one e-mail address.");
        var bad = emails.FirstOrDefault(e => !EmailPattern.IsMatch(e));
        if (bad != null) throw new Exception($"\"{bad}\" is not a valid e-mail address.");

        var isNew = string.IsNullOrEmpty(request.Id);
        var r = isNew ? new ScheduledReport { CreatedById = request.UpdatedById } : await _reports.GetAsync(request.Id!, ct) ?? throw new Exception("Report not found.");
        r.UpdatedById = request.UpdatedById;
        r.Name = request.Name!.Trim();
        r.ReportType = request.ReportType;
        r.Frequency = request.Frequency;
        r.DayOfWeek = request.Frequency == "Weekly" ? request.DayOfWeek : null;
        r.DayOfMonth = request.Frequency == "Monthly" ? request.DayOfMonth : null;
        r.SendHour = request.SendHour;
        r.SendMinute = request.SendMinute;
        r.Recipients = string.Join(", ", emails);
        r.Enabled = request.Enabled;
        r.WarehouseId = string.IsNullOrWhiteSpace(request.WarehouseId) ? null : request.WarehouseId;
        r.SlowDays = request.SlowDays is > 0 ? request.SlowDays : null;

        if (isNew) await _reports.CreateAsync(r, ct); else _reports.Update(r);
        await _unitOfWork.SaveAsync(ct);
        return r.Id;
    }
}

public class DeleteScheduledReportRequest : IRequest<bool>
{
    public string? Id { get; init; }
    public string? UpdatedById { get; init; }
}

public class DeleteScheduledReportHandler : IRequestHandler<DeleteScheduledReportRequest, bool>
{
    private readonly ICommandRepository<ScheduledReport> _reports;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteScheduledReportHandler(ICommandRepository<ScheduledReport> reports, IUnitOfWork unitOfWork)
    {
        _reports = reports;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteScheduledReportRequest request, CancellationToken ct)
    {
        var r = await _reports.GetAsync(request.Id ?? string.Empty, ct) ?? throw new Exception("Report not found.");
        r.UpdatedById = request.UpdatedById;
        _reports.Delete(r);
        await _unitOfWork.SaveAsync(ct);
        return true;
    }
}

// ---------------------------------------------------------------- sending
public class RunScheduledReportsResult
{
    public int Sent { get; set; }
    public string Message { get; set; } = "";
    public List<string> Problems { get; set; } = new();
}

public class RunScheduledReportsRequest : IRequest<RunScheduledReportsResult>
{
    /// <summary>Send this one report now regardless of its schedule; leave empty to send whatever is due.</summary>
    public string? Id { get; init; }
    public int OffsetMinutes { get; init; }
}

public class RunScheduledReportsHandler : IRequestHandler<RunScheduledReportsRequest, RunScheduledReportsResult>
{
    private readonly ISender _sender;
    private readonly ICommandRepository<ScheduledReport> _reports;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;

    public RunScheduledReportsHandler(ISender sender, ICommandRepository<ScheduledReport> reports, IUnitOfWork unitOfWork, IEmailService email)
    {
        _sender = sender;
        _reports = reports;
        _unitOfWork = unitOfWork;
        _email = email;
    }

    public async Task<RunScheduledReportsResult> Handle(RunScheduledReportsRequest request, CancellationToken ct)
    {
        var result = new RunScheduledReportsResult();
        var now = DateTime.UtcNow;
        var all = _reports.GetQuery().ApplyIsDeletedFilter().ToList();
        var targets = string.IsNullOrEmpty(request.Id)
            ? all.Where(r =>
            {
                if (!r.Enabled) return false;
                var lower = r.LastSentUtc ?? r.UpdatedAtUtc ?? r.CreatedAtUtc ?? DateTime.MinValue;
                return Schedule.LastOccurrenceUtc(r, now, request.OffsetMinutes) > lower;
            }).ToList()
            : all.Where(r => r.Id == request.Id).ToList();
        if (targets.Count == 0)
        {
            result.Message = string.IsNullOrEmpty(request.Id) ? "No report is due." : "Report not found.";
            return result;
        }

        foreach (var r in targets)
        {
            try
            {
                var (subject, html, _, fileName, csv) = await ReportEmailBuilder.Build(_sender, r.ReportType ?? "", r.WarehouseId, r.SlowDays ?? 90, r.Name ?? "Report", ct);
                var to = (r.Recipients ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var attachments = new List<EmailAttachment> { new EmailAttachment { FileName = fileName, Content = csv, ContentType = "text/csv" } };
                foreach (var addr in to) await _email.SendEmailWithAttachmentsAsync(addr, subject, html, attachments);
                r.LastStatus = $"Sent to {to.Length} recipient(s)";
                result.Sent++;
            }
            catch (Exception ex)
            {
                r.LastStatus = ("Failed: " + ex.Message);
                if (r.LastStatus.Length > 250) r.LastStatus = r.LastStatus.Substring(0, 250);
                result.Problems.Add($"{r.Name}: {ex.Message}");
            }
            // mark the attempt either way so a broken mail setup does not retry every few minutes
            r.LastSentUtc = now;
            _reports.Update(r);
        }
        await _unitOfWork.SaveAsync(ct);
        result.Message = $"{result.Sent} report(s) sent" + (result.Problems.Count > 0 ? $", {result.Problems.Count} failed." : ".");
        return result;
    }
}
