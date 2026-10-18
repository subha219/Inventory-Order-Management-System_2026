using Domain.Common;

namespace Domain.Entities;

/// <summary>One recorded change to a business record (who, when, what). Written automatically on every save.</summary>
public class AuditLog : BaseEntity
{
    public string? UserId { get; set; }
    /// <summary>Created, Updated or Deleted.</summary>
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    /// <summary>Human-friendly label such as the record's number or name.</summary>
    public string? EntityLabel { get; set; }
    /// <summary>JSON: for updates { field: { old, new } }, for new records the main field values.</summary>
    public string? Changes { get; set; }
}
