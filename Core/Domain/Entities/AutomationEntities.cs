using Domain.Common;

namespace Domain.Entities;

/// <summary>A watch on something in the warehouse: when it is true, people are told in the app and optionally by e-mail.</summary>
public class AutomationRule : BaseEntity
{
    /// <summary>LowStock, ExpiringBatches, ExpiredBatches, Overcommitted, OldBackorders or PendingApprovals.</summary>
    public string? RuleType { get; set; }
    public string? Name { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Days or hours depending on the rule; not used by every rule.</summary>
    public double? Threshold { get; set; }
    public bool NotifyInApp { get; set; } = true;
    /// <summary>E-mail addresses separated by commas or semicolons; empty means no e-mail.</summary>
    public string? EmailRecipients { get; set; }
    public DateTime? LastCheckedUtc { get; set; }
    public int? LastCount { get; set; }
    public string? LastEmailDay { get; set; }
}

/// <summary>A message in the bell menu, raised by an automation rule. One per rule per day.</summary>
public class AppNotification : BaseEntity
{
    public string? RuleId { get; set; }
    public string? RuleType { get; set; }
    /// <summary>info, warning or critical.</summary>
    public string? Severity { get; set; }
    public string? Title { get; set; }
    public string? Message { get; set; }
    public string? Link { get; set; }
    public int? ItemCount { get; set; }
    /// <summary>yyyyMMdd of the day it was raised.</summary>
    public string? DayKey { get; set; }
    /// <summary>User ids that have read it, written as ;id;id;</summary>
    public string? ReadByIds { get; set; }
}
