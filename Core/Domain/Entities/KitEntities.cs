using Domain.Common;

namespace Domain.Entities;

/// <summary>One component of a kit: building one kit uses Quantity units of the component product.</summary>
public class ProductBomLine : BaseEntity
{
    public string? KitProductId { get; set; }
    public string? ComponentProductId { get; set; }
    public double? Quantity { get; set; }
}

/// <summary>A record that kits were built: the components were taken out of stock and the finished kits put in.</summary>
public class KitAssembly : BaseEntity
{
    public string? Number { get; set; }
    public string? KitProductId { get; set; }
    public string? WarehouseId { get; set; }
    public double? Quantity { get; set; }
    public string? ComponentsDocument { get; set; }
    public string? KitsDocument { get; set; }
    public string? Notes { get; set; }
}
