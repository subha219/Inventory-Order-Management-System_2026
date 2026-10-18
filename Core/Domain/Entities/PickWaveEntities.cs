using Domain.Common;

namespace Domain.Entities;

/// <summary>A pick run that covers several delivery orders at once, so one walk through the warehouse serves them all.</summary>
public class PickWave : BaseEntity
{
    public string? Number { get; set; }
    /// <summary>Open, Picked or Cancelled.</summary>
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A delivery order that belongs to a wave.</summary>
public class PickWaveOrder : BaseEntity
{
    public string? PickWaveId { get; set; }
    public string? DeliveryOrderId { get; set; }
}
