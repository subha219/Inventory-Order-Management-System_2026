using Domain.Common;

namespace Domain.Entities;

/// <summary>Result of checking the goods that arrived on a goods receipt, one record per receipt and product.</summary>
public class QualityInspection : BaseEntity
{
    public string? GoodsReceiveId { get; set; }
    public string? ProductId { get; set; }
    public double? PassedQuantity { get; set; }
    public double? FailedQuantity { get; set; }
    /// <summary>Pass, Fail or Partial.</summary>
    public string? Result { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Units of a received product that were put away into a storage location.</summary>
public class PutAwayRecord : BaseEntity
{
    public string? GoodsReceiveId { get; set; }
    public string? ProductId { get; set; }
    public string? LocationId { get; set; }
    public double? Quantity { get; set; }
}
