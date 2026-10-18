using Domain.Common;

namespace Domain.Entities;

public class Product : BaseEntity
{
    public string? Name { get; set; }
    public string? Number { get; set; }
    public string? Description { get; set; }
    public double? UnitPrice { get; set; }
    /// <summary>Stock level at or below which the product is flagged as "low stock". Null = not tracked.</summary>
    public double? ReorderPoint { get; set; }
    /// <summary>Suggested quantity to order when the product hits its reorder point.</summary>
    public double? ReorderQuantity { get; set; }
    public bool? Physical { get; set; } = true;
    public string? UnitMeasureId { get; set; }
    public UnitMeasure? UnitMeasure { get; set; }
    public string? ProductGroupId { get; set; }
    public ProductGroup? ProductGroup { get; set; }
}
