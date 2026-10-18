namespace InventraUI.BackEnd.Common.Services;

public class ReportSettings
{
    /// <summary>Minutes east of UTC that report times are written in. 330 = India (UTC+05:30).</summary>
    public int TimeZoneOffsetMinutes { get; set; } = 330;
}
