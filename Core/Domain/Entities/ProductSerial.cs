using Domain.Common;

namespace Domain.Entities;

/// <summary>One individually tracked unit of a product, identified by its serial number.</summary>
public class ProductSerial : BaseEntity
{
    public string? ProductId { get; set; }
    public Product? Product { get; set; }
    /// <summary>Unique per product.</summary>
    public string? SerialNumber { get; set; }
    /// <summary>InStock, Shipped, Returned or Scrapped.</summary>
    public string? Status { get; set; }
    public string? WarehouseId { get; set; }
    public string? LocationId { get; set; }
    public string? GoodsReceiveId { get; set; }
    public string? GoodsReceiveNumber { get; set; }
    public string? DeliveryOrderId { get; set; }
    public string? DeliveryOrderNumber { get; set; }
    public string? CustomerName { get; set; }
    public DateTime? ShippedAtUtc { get; set; }
    public string? Notes { get; set; }
}
