using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.PermissionManager;

public class PermissionRuleDto
{
    public string? RoleName { get; init; }
    public string? Module { get; init; }
    public string? ActionKey { get; init; }
}

/// <summary>All deny rules, or just those of one role.</summary>
public class GetPermissionRulesRequest : IRequest<List<PermissionRuleDto>>
{
    public string? RoleName { get; init; }
}

public class GetPermissionRulesHandler : IRequestHandler<GetPermissionRulesRequest, List<PermissionRuleDto>>
{
    private readonly IQueryContext _context;

    public GetPermissionRulesHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<List<PermissionRuleDto>> Handle(GetPermissionRulesRequest request, CancellationToken ct)
    {
        var q = _context.PermissionRule.AsNoTracking().ApplyIsDeletedFilter(false);
        if (!string.IsNullOrEmpty(request.RoleName)) q = q.Where(x => x.RoleName == request.RoleName);
        return await q.Select(x => new PermissionRuleDto { RoleName = x.RoleName, Module = x.Module, ActionKey = x.ActionKey }).ToListAsync(ct);
    }
}

public class SavePermissionRulesRequest : IRequest<int>
{
    public string? RoleName { get; init; }
    /// <summary>The complete list of denials for this role; it replaces what was saved before.</summary>
    public List<PermissionRuleDto>? Denies { get; init; }
    public string? UpdatedById { get; init; }
}

public class SavePermissionRulesHandler : IRequestHandler<SavePermissionRulesRequest, int>
{
    private static readonly string[] Keys = { "Create", "Edit", "Confirm", "Cancel", "Delete" };
    private readonly ICommandRepository<PermissionRule> _rules;
    private readonly IUnitOfWork _unitOfWork;

    public SavePermissionRulesHandler(ICommandRepository<PermissionRule> rules, IUnitOfWork unitOfWork)
    {
        _rules = rules; _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(SavePermissionRulesRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName)) throw new Exception("Choose a role.");
        foreach (var old in _rules.GetQuery().ApplyIsDeletedFilter().Where(x => x.RoleName == request.RoleName).ToList()) _rules.Delete(old);

        var seen = new HashSet<string>();
        var count = 0;
        foreach (var d in request.Denies ?? new List<PermissionRuleDto>())
        {
            if (string.IsNullOrWhiteSpace(d.Module) || !Keys.Contains(d.ActionKey ?? "")) continue;
            if (!seen.Add(d.Module + "|" + d.ActionKey)) continue;
            await _rules.CreateAsync(new PermissionRule { CreatedById = request.UpdatedById, RoleName = request.RoleName, Module = d.Module, ActionKey = d.ActionKey }, ct);
            count++;
        }
        await _unitOfWork.SaveAsync(ct);
        return count;
    }
}
