using Domain.Common;

namespace Domain.Entities;

/// <summary>Takes one action away from a role in one module (for example: role Sales may not Delete in SalesOrder). Everything not listed stays allowed.</summary>
public class PermissionRule : BaseEntity
{
    public string? RoleName { get; set; }
    /// <summary>The controller name, e.g. SalesOrder.</summary>
    public string? Module { get; set; }
    /// <summary>Create, Edit, Confirm, Cancel or Delete.</summary>
    public string? ActionKey { get; set; }
}
