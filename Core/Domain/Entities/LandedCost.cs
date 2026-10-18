using Domain.Common;

namespace Domain.Entities;

/// <summary>An extra cost of getting goods in (freight, duty, insurance) that is added to the cost of a goods receipt.</summary>
public class LandedCost : BaseEntity
{
    public string? GoodsReceiveId { get; set; }
    public string? Description { get; set; }
    public double? Amount { get; set; }
    /// <summary>How the amount is shared over the receipt lines: Value (by purchase value) or Quantity (by units).</summary>
    public string? Basis { get; set; }
}
