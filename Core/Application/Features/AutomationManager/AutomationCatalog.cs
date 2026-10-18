namespace Application.Features.AutomationManager;

public class RuleDefinition
{
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>What the threshold means ("days", "hours") or null when the rule has none.</summary>
    public string? ThresholdUnit { get; init; }
    public double DefaultThreshold { get; init; }
    public string Link { get; init; } = "";
    public string Severity { get; init; } = "warning";
}

public static class AutomationCatalog
{
    public static readonly List<RuleDefinition> All = new()
    {
        new() { Type = "LowStock", Name = "Low stock", Description = "Products at or below their reorder point.", Link = "/PurchaseOrders/ReorderSuggestions", Severity = "warning" },
        new() { Type = "ExpiringBatches", Name = "Batches expiring soon", Description = "Batches with stock that expire within the number of days you set.", ThresholdUnit = "days", DefaultThreshold = 30, Link = "/Products/ProductBatches", Severity = "warning" },
        new() { Type = "ExpiredBatches", Name = "Expired batches", Description = "Batches that have expired but still show stock.", Link = "/Products/ProductBatches", Severity = "critical" },
        new() { Type = "Overcommitted", Name = "Overcommitted stock", Description = "Products promised to customers beyond what is on hand.", Link = "/Products/StockAvailability", Severity = "critical" },
        new() { Type = "OldBackorders", Name = "Old backorders", Description = "Customer order lines still short of stock after the number of days you set.", ThresholdUnit = "days", DefaultThreshold = 7, Link = "/SalesOrders/Backorders", Severity = "warning" },
        new() { Type = "PendingApprovals", Name = "Approvals waiting", Description = "Stock adjustments waiting for an approver longer than the hours you set (0 = any).", ThresholdUnit = "hours", DefaultThreshold = 24, Link = "/Users/Approvals", Severity = "info" }
    };
}
