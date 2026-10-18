using Domain.Common;

namespace Domain.Entities;

/// <summary>A loading dock door in a warehouse.</summary>
public class DockDoor : BaseEntity
{
    public string? Name { get; set; }
    public string? WarehouseId { get; set; }
    /// <summary>Inbound, Outbound or Both.</summary>
    public string? DoorType { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>A booked truck slot at a dock door.</summary>
public class DockAppointment : BaseEntity
{
    public string? Number { get; set; }
    public string? DockDoorId { get; set; }
    /// <summary>Inbound or Outbound.</summary>
    public string? Direction { get; set; }
    public DateTime? StartAtUtc { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Carrier { get; set; }
    public string? Reference { get; set; }
    public string? PartyName { get; set; }
    /// <summary>Scheduled, CheckedIn, Completed, Cancelled, NoShow.</summary>
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
}
