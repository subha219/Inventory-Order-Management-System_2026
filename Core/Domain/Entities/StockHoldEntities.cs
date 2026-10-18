using Domain.Common;

namespace Domain.Entities;

/// <summary>Units set aside in a warehouse so they cannot be picked or promised: quality check, damage, recall and so on.</summary>
public class StockHold : BaseEntity
{
    public string? ProductId { get; set; }
    public string? WarehouseId { get; set; }
    /// <summary>Units still on hold.</summary>
    public double? Quantity { get; set; }
    public double? OriginalQuantity { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    /// <summary>Active or Released.</summary>
    public string? Status { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public string? ReleasedById { get; set; }
}
