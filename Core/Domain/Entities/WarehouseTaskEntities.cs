using Domain.Common;

namespace Domain.Entities;

/// <summary>A piece of floor work handed to a person: pick, put away, count, move, pack and so on.</summary>
public class WarehouseTask : BaseEntity
{
    public string? Number { get; set; }
    /// <summary>Pick, PutAway, Count, Move, Receive, Pack or Other.</summary>
    public string? TaskType { get; set; }
    public string? Title { get; set; }
    /// <summary>A document number or bin the task relates to.</summary>
    public string? Reference { get; set; }
    public string? WarehouseId { get; set; }
    /// <summary>Low, Normal, High or Urgent.</summary>
    public string? Priority { get; set; }
    public string? AssignedToId { get; set; }
    /// <summary>Open, InProgress, Done or Cancelled.</summary>
    public string? Status { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Notes { get; set; }
}
