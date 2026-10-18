using System.Net;
using System.Text;
using Application.Features.BackorderManager.Queries;
using Application.Features.ProductManager.Queries;
using Application.Features.StockReportManager.Queries;
using MediatR;

namespace Application.Features.ScheduledReportManager;

public class ReportTypeInfo
{
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool UsesWarehouse { get; init; }
    public bool UsesSlowDays { get; init; }
}

public static class ReportCatalog
{
    public static readonly List<ReportTypeInfo> All = new()
    {
        new() { Type = "InventoryValuation", Name = "Inventory valuation", Description = "Total stock value, value by product group and the most valuable products.", UsesWarehouse = true },
        new() { Type = "SlowMoving", Name = "Slow-moving and unsold stock", Description = "Products with stock that have not sold for a while, with the value tied up.", UsesWarehouse = true, UsesSlowDays = true },
        new() { Type = "StockAvailability", Name = "Stock availability", Description = "On hand, reserved for customers and incoming, with the products that are overcommitted or low." },
        new() { Type = "Backorders", Name = "Backorders", Description = "Customer order lines waiting for stock and what still has to be ordered from vendors." }
    };
}

/// <summary>Builds the e-mail body for each report type from the same queries the report pages use.</summary>
public static class ReportEmailBuilder
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string N(double v) => v.ToString("#,0.##");

    private static string Table(string[] heads, IEnumerable<string[]> rows, int[]? rightAligned = null)
    {
        var sb = new StringBuilder("<table cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;border:1px solid #ddd;font-size:13px\"><tr style=\"background:#f3f3f3\">");
        for (var i = 0; i < heads.Length; i++) sb.Append($"<th align=\"{(rightAligned?.Contains(i) == true ? "right" : "left")}\">{E(heads[i])}</th>");
        sb.Append("</tr>");
        foreach (var r in rows)
        {
            sb.Append("<tr style=\"border-top:1px solid #ddd\">");
            for (var i = 0; i < r.Length; i++) sb.Append($"<td align=\"{(rightAligned?.Contains(i) == true ? "right" : "left")}\">{E(r[i])}</td>");
            sb.Append("</tr>");
        }
        return sb.Append("</table>").ToString();
    }

        private static string Csv(string[] heads, IEnumerable<string[]> rows)
    {
        static string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.Append(string.Join(",", heads.Select(Q))).Append("\r\n");
        foreach (var r in rows) sb.Append(string.Join(",", r.Select(Q))).Append("\r\n");
        return sb.ToString();
    }

    private static string R(double v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    public static async Task<(string Subject, string Html, bool Empty, string FileName, byte[] Csv)> Build(ISender sender, string type, string? warehouseId, int slowDays, string name, CancellationToken ct)
    {
        var stamp = DateTime.UtcNow.ToString("dd MMM yyyy");
        var body = new StringBuilder("<div style=\"font-family:Arial,sans-serif;font-size:14px\">");
        var empty = false;
        string csv = "";
        var fileBase = type switch { "InventoryValuation" => "inventory-valuation", "SlowMoving" => "slow-moving-stock", "StockAvailability" => "stock-availability", _ => "backorders" };

        switch (type)
        {
            case "InventoryValuation":
            {
                var r = await sender.Send(new GetInventoryInsightsRequest { WarehouseId = warehouseId, SlowDays = slowDays }, ct);
                body.Append($"<p>Stock value is <strong>{N(r.TotalValue)}</strong> across {N(r.TotalUnits)} units in {r.Rows.Count} products.</p>");
                body.Append("<p><strong>By product group</strong></p>").Append(Table(new[] { "Group", "Products", "Units", "Value" },
                    r.Groups.Take(20).Select(g => new[] { g.GroupName ?? "", g.Products.ToString(), N(g.Units), N(g.Value) }), new[] { 1, 2, 3 }));
                body.Append("<p><strong>Most valuable products</strong></p>").Append(Table(new[] { "Product", "On hand", "Unit price", "Value" },
                    r.Rows.Take(30).Select(x => new[] { $"{x.ProductName} ({x.ProductNumber})", N(x.OnHand), N(x.UnitPrice), N(x.Value) }), new[] { 1, 2, 3 }));
                empty = r.Rows.Count == 0;
                csv = Csv(new[] { "Product number", "Product", "Group", "On hand", "Unit price", "Value", "Last sold", "Movement" },
                    r.Rows.Select(x => new[] { x.ProductNumber ?? "", x.ProductName ?? "", x.GroupName ?? "", R(x.OnHand), R(x.UnitPrice), R(x.Value), x.LastSold?.ToString("yyyy-MM-dd") ?? "Never", x.Movement ?? "" }));
                break;
            }
            case "SlowMoving":
            {
                var r = await sender.Send(new GetInventoryInsightsRequest { WarehouseId = warehouseId, SlowDays = slowDays }, ct);
                var rows = r.Rows.Where(x => x.Movement != "Active").OrderByDescending(x => x.Value).ToList();
                body.Append($"<p><strong>{r.SlowCount}</strong> product(s) have not sold for {slowDays}+ days (value {N(r.SlowValue)}) and <strong>{r.DeadCount}</strong> have never sold (value {N(r.DeadValue)}).</p>");
                body.Append(Table(new[] { "Product", "On hand", "Value", "Last sold", "Status" },
                    rows.Take(50).Select(x => new[] { $"{x.ProductName} ({x.ProductNumber})", N(x.OnHand), N(x.Value), x.LastSold.HasValue ? $"{x.DaysSinceSold}d ago" : "Never", x.Movement ?? "" }), new[] { 1, 2 }));
                if (rows.Count > 50) body.Append($"<p>...and {rows.Count - 50} more.</p>");
                empty = rows.Count == 0;
                csv = Csv(new[] { "Product number", "Product", "Group", "On hand", "Value", "Last sold", "Days since sold", "Movement" },
                    rows.Select(x => new[] { x.ProductNumber ?? "", x.ProductName ?? "", x.GroupName ?? "", R(x.OnHand), R(x.Value), x.LastSold?.ToString("yyyy-MM-dd") ?? "Never", x.DaysSinceSold?.ToString() ?? "", x.Movement ?? "" }));
                break;
            }
            case "StockAvailability":
            {
                var r = await sender.Send(new GetStockAvailabilityRequest(), ct);
                var rows = (r.Data ?? new List<StockAvailabilityDto>()).Where(x => x.State != "Ok").ToList();
                body.Append($"<p>{r.Products} products with stock or orders. <strong>{r.Overcommitted}</strong> overcommitted, <strong>{r.Low}</strong> low or short. Reserved for customers: {N(r.TotalReserved)}; incoming: {N(r.TotalIncoming)}.</p>");
                body.Append(Table(new[] { "Product", "On hand", "Reserved", "Available", "Incoming", "Status" },
                    rows.Take(50).Select(x => new[] { $"{x.Name} ({x.Number})", N(x.OnHand), N(x.Reserved), N(x.Available), N(x.Incoming), x.State ?? "" }), new[] { 1, 2, 3, 4 }));
                empty = rows.Count == 0;
                csv = Csv(new[] { "Product number", "Product", "On hand", "Reserved", "Available", "Incoming", "Projected", "Status" },
                    (r.Data ?? new List<StockAvailabilityDto>()).Select(x => new[] { x.Number ?? "", x.Name ?? "", R(x.OnHand), R(x.Reserved), R(x.Available), R(x.Incoming), R(x.Projected), x.State ?? "" }));
                break;
            }
            case "Backorders":
            {
                var r = await sender.Send(new GetBackordersRequest(), ct);
                body.Append($"<p><strong>{r.OrdersAffected}</strong> customer order(s) are short of stock: {r.LinesBackordered} line(s), {N(r.UnitsShort)} units, the oldest waiting {r.OldestDays} days.</p>");
                if (r.Products.Count > 0)
                {
                    body.Append("<p><strong>What to order</strong></p>").Append(Table(new[] { "Product", "On hand", "Short", "Already on order", "Still to order" },
                        r.Products.Take(30).Select(x => new[] { $"{x.ProductName} ({x.ProductNumber})", N(x.OnHand), N(x.Short), N(x.Incoming), N(x.ToOrder) }), new[] { 1, 2, 3, 4 }));
                }
                var lines = r.Lines.Where(l => l.Short > 0).ToList();
                if (lines.Count > 0)
                {
                    body.Append("<p><strong>Orders waiting</strong></p>").Append(Table(new[] { "Order", "Customer", "Product", "Short", "Waiting" },
                        lines.Take(30).Select(l => new[] { l.OrderNumber ?? "", l.Customer ?? "", l.ProductName ?? "", N(l.Short), $"{l.DaysWaiting}d" }), new[] { 3, 4 }));
                }
                empty = lines.Count == 0;
                csv = Csv(new[] { "Order", "Customer", "Days waiting", "Product number", "Product", "Ordered", "Delivered", "Outstanding", "Can ship now", "Short", "Covered by incoming", "Status" },
                    r.Lines.Select(l => new[] { l.OrderNumber ?? "", l.Customer ?? "", l.DaysWaiting.ToString(), l.ProductNumber ?? "", l.ProductName ?? "", R(l.Ordered), R(l.Delivered), R(l.Outstanding), R(l.CanShipNow), R(l.Short), R(l.CoveredByIncoming), l.State ?? "" }));
                break;
            }
            default:
                throw new Exception($"Unknown report type {type}.");
        }

        body.Append("<p style=\"color:#888;font-size:12px\">The complete data is attached as a CSV file. Sent automatically by Inventra.</p></div>");
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        return ($"[Inventra] {name} - {stamp}", body.ToString(), empty, $"{fileBase}-{DateTime.UtcNow:yyyy-MM-dd}.csv", bytes);
    }
}
