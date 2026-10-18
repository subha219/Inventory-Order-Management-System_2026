using Domain.Common;

namespace Domain.Entities;

/// <summary>A report that is e-mailed to a list of people on a schedule.</summary>
public class ScheduledReport : BaseEntity
{
    public string? Name { get; set; }
    /// <summary>InventoryValuation, SlowMoving, StockAvailability or Backorders.</summary>
    public string? ReportType { get; set; }
    /// <summary>Daily, Weekly or Monthly.</summary>
    public string? Frequency { get; set; }
    /// <summary>0 = Sunday ... 6 = Saturday, used when weekly.</summary>
    public int? DayOfWeek { get; set; }
    /// <summary>1 to 31, used when monthly (short months use their last day).</summary>
    public int? DayOfMonth { get; set; }
    public int SendHour { get; set; }
    public int SendMinute { get; set; }
    public string? Recipients { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Limit valuation and slow-moving reports to one warehouse; empty means all.</summary>
    public string? WarehouseId { get; set; }
    /// <summary>Days without a sale before a product counts as slow-moving.</summary>
    public int? SlowDays { get; set; }
    public DateTime? LastSentUtc { get; set; }
    public string? LastStatus { get; set; }
}
