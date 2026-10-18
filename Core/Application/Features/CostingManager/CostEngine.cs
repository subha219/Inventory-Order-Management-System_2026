namespace Application.Features.CostingManager;

/// <summary>One stock movement used for costing. Qty is signed: plus for stock in, minus for stock out.</summary>
public class CostMove
{
    public string ProductId { get; set; } = "";
    public DateTime When { get; set; }
    public double Qty { get; set; }
    public string? ModuleName { get; set; }
    public string? ModuleId { get; set; }
}

public class CostResult
{
    public double OnHand { get; set; }
    public double AverageCost { get; set; }
    public double AverageValue { get; set; }
    public double FifoCost { get; set; }
    public double FifoValue { get; set; }
    /// <summary>True when some stock had no purchase cost and the product price was used instead.</summary>
    public bool Estimated { get; set; }
}

/// <summary>
/// Works out stock cost from the movement history, under both methods at once.
/// Average: a moving average that is recalculated at every receipt. FIFO: oldest cost layers are used up first.
/// Receipts cost what the purchase order said plus their share of landed costs; other stock in (adjustments, returns,
/// transfers) enters at the running average cost, or at the product price when nothing is known yet.
/// </summary>
public static class CostEngine
{
    public static CostResult Calculate(IEnumerable<CostMove> moves, Func<string?, string?, double?> receiptUnitCost, double fallbackCost)
    {
        double avgQty = 0, avg = 0, lastKnown = 0;
        var layers = new LinkedList<(double Qty, double Cost)>();
        var estimated = false;
        double onHand = 0;

        foreach (var m in moves.OrderBy(x => x.When))
        {
            onHand += m.Qty;
            if (m.Qty > 0)
            {
                double? known = m.ModuleName == "GoodsReceive" ? receiptUnitCost(m.ModuleId, m.ProductId) : null;
                double cost;
                if (known.HasValue) { cost = known.Value; lastKnown = cost; }
                else if (avgQty > 1e-9) cost = avg;
                else if (lastKnown > 0) cost = lastKnown;
                else { cost = fallbackCost; estimated = true; }

                avg = avgQty + m.Qty <= 1e-9 ? cost : (avgQty * avg + m.Qty * cost) / (avgQty + m.Qty);
                avgQty += m.Qty;
                layers.AddLast((m.Qty, cost));
            }
            else if (m.Qty < 0)
            {
                var take = -m.Qty;
                avgQty = Math.Max(avgQty - take, 0);
                while (take > 1e-9 && layers.First != null)
                {
                    var f = layers.First.Value;
                    if (f.Qty <= take + 1e-9) { take -= f.Qty; layers.RemoveFirst(); }
                    else { layers.First.Value = (f.Qty - take, f.Cost); take = 0; }
                }
            }
        }

        var fifoQty = layers.Sum(l => l.Qty);
        var fifoValue = layers.Sum(l => l.Qty * l.Cost);
        return new CostResult
        {
            OnHand = onHand,
            AverageCost = avg,
            AverageValue = avgQty * avg,
            FifoCost = fifoQty > 1e-9 ? fifoValue / fifoQty : avg,
            FifoValue = fifoValue,
            Estimated = estimated && onHand > 1e-9
        };
    }
}
