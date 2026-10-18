using Domain.Common;

namespace Domain.Entities;

/// <summary>How many units of a product have been put away in a storage location.</summary>
public class LocationStock : BaseEntity
{
    public string? LocationId { get; set; }
    public StorageLocation? Location { get; set; }
    public string? ProductId { get; set; }
    public Product? Product { get; set; }
    public double? Quantity { get; set; }
}
