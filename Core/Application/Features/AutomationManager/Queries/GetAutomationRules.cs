using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.AutomationManager.Queries;

public class AutomationRuleDto
{
    public string? Id { get; init; }
    public string? RuleType { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? ThresholdUnit { get; init; }
    public bool Enabled { get; init; }
    public double? Threshold { get; init; }
    public bool NotifyInApp { get; init; }
    public string? EmailRecipients { get; init; }
    public DateTime? LastChecked { get; init; }
    public int? LastCount { get; init; }
    public string? Severity { get; init; }
}

public class GetAutomationRulesResult
{
    public List<AutomationRuleDto> Data { get; init; } = new();
}

public class GetAutomationRulesRequest : IRequest<GetAutomationRulesResult>
{
}

/// <summary>Lists the rules, creating the standard set the first time (all on, in-app only).</summary>
public class GetAutomationRulesHandler : IRequestHandler<GetAutomationRulesRequest, GetAutomationRulesResult>
{
    private readonly ICommandRepository<AutomationRule> _rules;
    private readonly IUnitOfWork _unitOfWork;

    public GetAutomationRulesHandler(ICommandRepository<AutomationRule> rules, IUnitOfWork unitOfWork)
    {
        _rules = rules;
        _unitOfWork = unitOfWork;
    }

    public async Task<GetAutomationRulesResult> Handle(GetAutomationRulesRequest request, CancellationToken ct)
    {
        var rows = _rules.GetQuery().ApplyIsDeletedFilter().ToList();
        var added = false;
        foreach (var def in AutomationCatalog.All)
        {
            if (rows.Any(r => r.RuleType == def.Type)) continue;
            var rule = new AutomationRule
            {
                RuleType = def.Type, Name = def.Name, Enabled = true, NotifyInApp = true,
                Threshold = def.ThresholdUnit == null ? null : def.DefaultThreshold
            };
            await _rules.CreateAsync(rule, ct);
            rows.Add(rule);
            added = true;
        }
        if (added) await _unitOfWork.SaveAsync(ct);

        var data = AutomationCatalog.All
            .Select(def => new { def, rule = rows.First(r => r.RuleType == def.Type) })
            .Select(x => new AutomationRuleDto
            {
                Id = x.rule.Id, RuleType = x.def.Type, Name = x.def.Name, Description = x.def.Description, ThresholdUnit = x.def.ThresholdUnit,
                Enabled = x.rule.Enabled, Threshold = x.rule.Threshold, NotifyInApp = x.rule.NotifyInApp, EmailRecipients = x.rule.EmailRecipients,
                LastChecked = x.rule.LastCheckedUtc, LastCount = x.rule.LastCount, Severity = x.def.Severity
            }).ToList();
        return new GetAutomationRulesResult { Data = data };
    }
}
