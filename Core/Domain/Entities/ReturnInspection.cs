using Domain.Common;

namespace Domain.Entities;

/// <summary>What was found when goods came back on a customer return, per product: how many are good and how many are damaged.</summary>
public class ReturnInspection : BaseEntity
{
    public string? SalesReturnId { get; set; }
    public string? ProductId { get; set; }
    public double? ReturnedQuantity { get; set; }
    public double? GoodQuantity { get; set; }
    public double? DamagedQuantity { get; set; }
    public string? ReasonCode { get; set; }
    public string? Note { get; set; }
    /// <summary>Where the good units were put back, if a location was chosen.</summary>
    public string? LocationId { get; set; }
}
