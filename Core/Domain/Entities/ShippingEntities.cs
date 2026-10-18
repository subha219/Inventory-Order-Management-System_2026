using Domain.Common;

namespace Domain.Entities;

/// <summary>One outgoing shipment for a delivery order (an order can ship in several parts).</summary>
public class Shipment : BaseEntity
{
    public string? Number { get; set; }
    public string? DeliveryOrderId { get; set; }
    /// <summary>Packing, Shipped.</summary>
    public string? Status { get; set; }
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime? ShippedAtUtc { get; set; }
    public string? ShippedById { get; set; }
    /// <summary>Secret part of the customer tracking link.</summary>
    public string? PublicToken { get; set; }
}

/// <summary>A carton or pallet inside a shipment.</summary>
public class ShipmentPackage : BaseEntity
{
    public string? ShipmentId { get; set; }
    public int? PackageNumber { get; set; }
    public string? PackageType { get; set; }
    public double? WeightKg { get; set; }
    public double? LengthCm { get; set; }
    public double? WidthCm { get; set; }
    public double? HeightCm { get; set; }
}

/// <summary>Units of a product packed in a carton.</summary>
public class ShipmentPackageItem : BaseEntity
{
    public string? ShipmentId { get; set; }
    public string? ShipmentPackageId { get; set; }
    public string? ProductId { get; set; }
    public double? Quantity { get; set; }
}
