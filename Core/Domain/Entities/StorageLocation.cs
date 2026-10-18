using Domain.Common;

namespace Domain.Entities;

/// <summary>A place inside a warehouse where stock is kept: zone / aisle / rack / shelf / bin, with an optional capacity in units.</summary>
public class StorageLocation : BaseEntity
{
    public string? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    /// <summary>Unique within the warehouse, e.g. A-01-02-3-04.</summary>
    public string? Code { get; set; }
    public string? Zone { get; set; }
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Shelf { get; set; }
    public string? Bin { get; set; }
    /// <summary>Bin, Pallet, Floor or Overflow.</summary>
    public string? LocationType { get; set; }
    /// <summary>Maximum units this location can hold; null means no limit.</summary>
    public double? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
