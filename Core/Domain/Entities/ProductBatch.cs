using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// One received lot of a product, with its batch number and expiry date.
/// Stock itself still lives in InventoryTransaction; the page estimates how much of the current stock
/// sits in each batch by assuming first-expired-first-out.
/// </summary>
public class ProductBatch : BaseEntity
{
    public string? ProductId { get; set; }
    public Product? Product { get; set; }
    public string? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    /// <summary>Quantity received into this batch.</summary>
    public double? Quantity { get; set; }
    public string? Notes { get; set; }
}
