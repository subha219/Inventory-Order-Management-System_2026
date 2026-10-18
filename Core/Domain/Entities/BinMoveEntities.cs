using Domain.Common;

namespace Domain.Entities;

/// <summary>A record that units of a product were moved from one storage location to another in the same warehouse.</summary>
public class LocationMove : BaseEntity
{
    public string? ProductId { get; set; }
    public string? FromLocationId { get; set; }
    public string? ToLocationId { get; set; }
    public double? Quantity { get; set; }
    /// <summary>Move (done by hand) or Replenish (from a replenishment suggestion).</summary>
    public string? MoveType { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Keeps a pick location topped up: when the product falls below Min, refill it up to Max from other locations.</summary>
public class ReplenishmentRule : BaseEntity
{
    public string? LocationId { get; set; }
    public string? ProductId { get; set; }
    public double? MinQuantity { get; set; }
    public double? MaxQuantity { get; set; }
}
