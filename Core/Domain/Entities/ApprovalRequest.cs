using Domain.Common;

namespace Domain.Entities;

/// <summary>A request for a supervisor to approve confirming a stock adjustment (or scrapping) that is above the approval limit.</summary>
public class ApprovalRequest : BaseEntity
{
    /// <summary>PositiveAdjustment, NegativeAdjustment or Scrapping.</summary>
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityNumber { get; set; }
    /// <summary>Total units moved by the document when approval was requested.</summary>
    public double? Units { get; set; }
    /// <summary>Pending, Approved or Rejected.</summary>
    public string? Status { get; set; }
    public string? RequestedById { get; set; }
    public string? DecidedById { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}
