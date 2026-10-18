using Domain.Common;

namespace Domain.Entities;

/// <summary>A record that some units of a product were picked from a storage location for a delivery order.</summary>
public class DeliveryPick : BaseEntity
{
    public string? DeliveryOrderId { get; set; }
    public string? ProductId { get; set; }
    public string? LocationId { get; set; }
    public double? Quantity { get; set; }
}
