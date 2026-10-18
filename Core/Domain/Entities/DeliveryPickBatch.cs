using Domain.Common;

namespace Domain.Entities;

/// <summary>Records which batch units picked for a delivery order came from (for recalls and expiry control).</summary>
public class DeliveryPickBatch : BaseEntity
{
    public string? DeliveryOrderId { get; set; }
    public string? ProductId { get; set; }
    public string? BatchId { get; set; }
    public double? Quantity { get; set; }
}
