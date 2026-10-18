using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using System.Text.RegularExpressions;

namespace Application.Features.AutomationManager.Commands;

public class SaveAutomationRuleResult
{
    public string? Id { get; set; }
}

public class SaveAutomationRuleRequest : IRequest<SaveAutomationRuleResult>
{
    public string? Id { get; init; }
    public bool Enabled { get; init; }
    public double? Threshold { get; init; }
    public bool NotifyInApp { get; init; }
    public string? EmailRecipients { get; init; }
    public string? UpdatedById { get; init; }
}

public class SaveAutomationRuleValidator : AbstractValidator<SaveAutomationRuleRequest>
{
    public SaveAutomationRuleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Threshold).GreaterThanOrEqualTo(0).When(x => x.Threshold.HasValue);
    }
}

public class SaveAutomationRuleHandler : IRequestHandler<SaveAutomationRuleRequest, SaveAutomationRuleResult>
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private readonly ICommandRepository<AutomationRule> _rules;
    private readonly IUnitOfWork _unitOfWork;

    public SaveAutomationRuleHandler(ICommandRepository<AutomationRule> rules, IUnitOfWork unitOfWork)
    {
        _rules = rules;
        _unitOfWork = unitOfWork;
    }

    public async Task<SaveAutomationRuleResult> Handle(SaveAutomationRuleRequest request, CancellationToken ct)
    {
        var rule = await _rules.GetAsync(request.Id ?? string.Empty, ct) ?? throw new Exception("Rule not found.");

        var emails = (request.EmailRecipients ?? string.Empty)
            .Split(new[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var bad = emails.FirstOrDefault(e => !EmailPattern.IsMatch(e));
        if (bad != null) throw new Exception($"\"{bad}\" is not a valid e-mail address.");

        rule.UpdatedById = request.UpdatedById;
        rule.Enabled = request.Enabled;
        rule.NotifyInApp = request.NotifyInApp;
        var def = AutomationCatalog.All.FirstOrDefault(d => d.Type == rule.RuleType);
        if (def?.ThresholdUnit != null) rule.Threshold = request.Threshold ?? def.DefaultThreshold;
        rule.EmailRecipients = emails.Count == 0 ? null : string.Join(", ", emails);
        _rules.Update(rule);
        await _unitOfWork.SaveAsync(ct);
        return new SaveAutomationRuleResult { Id = rule.Id };
    }
}
