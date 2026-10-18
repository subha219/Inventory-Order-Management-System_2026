using Domain.Common;

namespace Domain.Entities;

/// <summary>A secret link that lets a vendor or customer look at one order and answer it, without an account.</summary>
public class PortalLink : BaseEntity
{
    /// <summary>PurchaseOrder or SalesOrder.</summary>
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Token { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>An answer sent through a portal link.</summary>
public class PortalResponse : BaseEntity
{
    public string? PortalLinkId { get; set; }
    /// <summary>Confirmed, Declined or Question.</summary>
    public string? Status { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string? Message { get; set; }
}
