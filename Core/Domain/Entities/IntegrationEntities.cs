using Domain.Common;

namespace Domain.Entities;

/// <summary>A key that lets an outside system call the read-only integration API. Only a hash is stored.</summary>
public class IntegrationApiKey : BaseEntity
{
    public string? Name { get; set; }
    /// <summary>First characters of the key, shown so people can tell keys apart.</summary>
    public string? KeyPrefix { get; set; }
    public string? KeyHash { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
}

/// <summary>A URL that receives signed event notifications.</summary>
public class WebhookEndpoint : BaseEntity
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Secret { get; set; }
    /// <summary>Comma separated event names, or * for all.</summary>
    public string? Events { get; set; }
    public bool? IsActive { get; set; }
    public string? LastStatus { get; set; }
    public DateTime? LastAtUtc { get; set; }
}
