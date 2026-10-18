using System.Globalization;
using System.Text.RegularExpressions;
using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ProductManager.Queries;

public class AskInventoryResult
{
    public string Answer { get; init; } = string.Empty;
    public List<string> Columns { get; init; } = new();
    public List<List<string>> Rows { get; init; } = new();
    public List<string> Suggestions { get; init; } = new();
}

public class AskInventoryRequest : IRequest<AskInventoryResult>
{
    public string? Question { get; init; }
}

/// <summary>
/// Rule-based "ask your inventory" assistant. No AI service: it recognises common question types by keywords
/// (low stock, stock of a product, top sellers, slow movers, run-out forecast, inventory value ...) and answers from the database.
/// </summary>
public class AskInventoryHandler : IRequestHandler<AskInventoryRequest, AskInventoryResult>
{
    private static readonly string[] Examples =
    {
        "What is low on stock?",
        "Which products are out of stock?",
        "Top selling products this month",
        "What will run out in 2 weeks?",
        "What expires this month?",
        "Slow moving products in the last 60 days",
        "Total inventory value"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","for","how","many","much","what","which","have","has","our","are","there","stock","quantity",
        "available","hand","left","show","tell","about","with","from","this","that","product","products","item","items","many"
    };

    private readonly IQueryContext _context;

    public AskInventoryHandler(IQueryContext context)
    {
        _context = context;
    }

    private sealed class P
    {
        public string Id { get; init; } = string.Empty;
        public string? Number { get; init; }
        public string? Name { get; init; }
        public double UnitPrice { get; init; }
        public double? ReorderPoint { get; init; }
    }

    private static string N(double v) => v.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private async Task<List<P>> LoadProducts(CancellationToken ct)
    {
        var rows = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Physical == true)
            .Select(x => new { x.Id, x.Number, x.Name, x.UnitPrice, x.ReorderPoint })
            .ToListAsync(ct);
        return rows.Select(x => new P { Id = x.Id, Number = x.Number, Name = x.Name, UnitPrice = x.UnitPrice ?? 0, ReorderPoint = x.ReorderPoint }).ToList();
    }

    private async Task<Dictionary<string, double>> LoadStock(CancellationToken ct)
    {
        var rows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed
                        && x.Warehouse!.SystemWarehouse == false
                        && x.Product!.Physical == true)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(ct);
        return rows.Where(x => x.ProductId != null).ToDictionary(x => x.ProductId!, x => x.Stock ?? 0.0);
    }

    private async Task<Dictionary<string, double>> LoadSold(DateTime since, CancellationToken ct)
    {
        var rows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed
                        && x.ModuleName == "DeliveryOrder"
                        && x.MovementDate >= since)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Stock) })
            .ToListAsync(ct);
        return rows.Where(x => x.ProductId != null).ToDictionary(x => x.ProductId!, x => Math.Abs(x.Qty ?? 0.0));
    }

    private static int? ExtractDays(string q)
    {
        var m = Regex.Match(q, @"(\d+)\s*(day|week|month)s?");
        if (m.Success)
        {
            var n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return m.Groups[2].Value switch { "week" => n * 7, "month" => n * 30, _ => n };
        }
        if (q.Contains("today")) return 1;
        if (q.Contains("this week")) return 7;
        if (q.Contains("this month")) return Math.Max(DateTime.UtcNow.Day, 1);
        if (q.Contains("this year")) return Math.Max(DateTime.UtcNow.DayOfYear, 1);
        return null;
    }

    private static bool Has(string q, params string[] words) => words.Any(w => q.Contains(w));

    public async Task<AskInventoryResult> Handle(AskInventoryRequest request, CancellationToken ct)
    {
        var q = (request.Question ?? string.Empty).Trim().ToLowerInvariant();
        if (q.Length == 0) return Help("Ask me something about your stock. For example:");

        // ---- top sellers ----
        if (Has(q, "top", "best", "most", "fast", "popular") && Has(q, "sell", "sold", "sale", "moving", "seller"))
        {
            var days = ExtractDays(q) ?? 30;
            var since = DateTime.UtcNow.Date.AddDays(-(days - 1));
            var sold = await LoadSold(since, ct);
            var products = (await LoadProducts(ct)).ToDictionary(p => p.Id);
            var top = sold.Where(s => products.ContainsKey(s.Key) && s.Value > 0)
                .OrderByDescending(s => s.Value).Take(10).ToList();
            if (top.Count == 0) return Simple($"No confirmed deliveries in the last {days} days.");
            return new AskInventoryResult
            {
                Answer = $"Top selling products in the last {days} day(s), by units delivered:",
                Columns = new() { "Product", "Units sold", "Est. revenue" },
                Rows = top.Select(t => new List<string> { products[t.Key].Name ?? "", N(t.Value), N(t.Value * products[t.Key].UnitPrice) }).ToList(),
                Suggestions = Examples.Take(3).ToList()
            };
        }

        // ---- slow movers ----
        if (Has(q, "slow", "dead stock", "not sold", "no sales", "unsold", "not moving", "idle"))
        {
            var days = ExtractDays(q) ?? 60;
            var since = DateTime.UtcNow.Date.AddDays(-(days - 1));
            var sold = await LoadSold(since, ct);
            var stock = await LoadStock(ct);
            var products = await LoadProducts(ct);
            var slow = products
                .Where(p => stock.TryGetValue(p.Id, out var s) && s > 0 && (!sold.TryGetValue(p.Id, out var d) || d <= 0))
                .Select(p => new { P = p, Stock = stock[p.Id] })
                .OrderByDescending(x => x.Stock * x.P.UnitPrice).Take(25).ToList();
            if (slow.Count == 0) return Simple($"Every product in stock has had a delivery in the last {days} days.");
            return new AskInventoryResult
            {
                Answer = $"{slow.Count} product(s) have stock but no deliveries in the last {days} days (highest tied-up value first):",
                Columns = new() { "Product", "In stock", "Stock value" },
                Rows = slow.Select(x => new List<string> { x.P.Name ?? "", N(x.Stock), N(x.Stock * x.P.UnitPrice) }).ToList(),
                Suggestions = Examples.Take(3).ToList()
            };
        }

        // ---- run out soon ----
        if (Has(q, "run out", "runout", "running out", "stock out", "stockout", "days of cover", "how long will", "last for"))
        {
            var horizon = ExtractDays(q) ?? 14;
            const int window = 30;
            var since = DateTime.UtcNow.Date.AddDays(-(window - 1));
            var sold = await LoadSold(since, ct);
            var stock = await LoadStock(ct);
            var products = await LoadProducts(ct);
            var soon = products
                .Where(p => sold.TryGetValue(p.Id, out var d) && d > 0)
                .Select(p =>
                {
                    var rate = sold[p.Id] / window;
                    var s = stock.TryGetValue(p.Id, out var st) ? Math.Max(st, 0) : 0;
                    return new { P = p, Stock = s, Rate = rate, Days = s / rate };
                })
                .Where(x => x.Days <= horizon)
                .OrderBy(x => x.Days).Take(25).ToList();
            if (soon.Count == 0) return Simple($"Nothing is forecast to run out within {horizon} days, based on the last {window} days of sales.");
            return new AskInventoryResult
            {
                Answer = $"{soon.Count} product(s) are forecast to run out within {horizon} days (based on the last {window} days of deliveries):",
                Columns = new() { "Product", "In stock", "Sold / day", "Days left" },
                Rows = soon.Select(x => new List<string> { x.P.Name ?? "", N(x.Stock), N(x.Rate), N(Math.Floor(x.Days)) }).ToList(),
                Suggestions = new() { "What is low on stock?", "Top selling products this month" }
            };
        }

        // ---- out of stock ----
        if (Has(q, "out of stock", "zero stock", "no stock", "stockout list", "sold out"))
        {
            var stock = await LoadStock(ct);
            var products = await LoadProducts(ct);
            var list = products.Where(p => stock.TryGetValue(p.Id, out var s) && s <= 0).Take(50).ToList();
            if (list.Count == 0) return Simple("No product is out of stock.");
            return new AskInventoryResult
            {
                Answer = $"{list.Count} product(s) are out of stock (products that never had stock are not listed):",
                Columns = new() { "Product", "Number", "Reorder point" },
                Rows = list.Select(p => new List<string> { p.Name ?? "", p.Number ?? "", p.ReorderPoint.HasValue ? N(p.ReorderPoint.Value) : "-" }).ToList(),
                Suggestions = new() { "What is low on stock?" }
            };
        }

        // ---- expiry / batches ----
        if (Has(q, "expir", "expire", "shelf life", "best before", "batch", "lot "))
        {
            var days = ExtractDays(q) ?? 30;
            var cutoff = DateTime.UtcNow.Date.AddDays(q.Contains("this month") ? 31 - Math.Min(DateTime.UtcNow.Day, 30) : days);
            var onlyExpired = Has(q, "already", "expired") && !Has(q, "expire ", "expires", "expiring");
            var batches = await _context.ProductBatch.AsNoTracking()
                .Where(b => !b.IsDeleted && b.ExpiryDate != null && (onlyExpired ? b.ExpiryDate < DateTime.UtcNow : b.ExpiryDate <= cutoff))
                .OrderBy(b => b.ExpiryDate)
                .Select(b => new { b.ProductId, b.BatchNumber, b.ExpiryDate, b.Quantity })
                .Take(50).ToListAsync(ct);
            if (batches.Count == 0) return Simple(onlyExpired ? "No recorded batches have expired." : "No recorded batches expire in that window. (Batches are added under Inventory → Batches & Expiry.)");
            var names = (await LoadProducts(ct)).ToDictionary(p => p.Id, p => p.Name);
            return new AskInventoryResult
            {
                Answer = onlyExpired
                    ? $"{batches.Count} batch(es) have already expired. Open Inventory → Batches & Expiry for the estimated units left."
                    : $"{batches.Count} batch(es) expire by {cutoff:dd MMM yyyy}. Open Inventory → Batches & Expiry for the estimated units left.",
                Columns = new() { "Product", "Batch", "Expiry", "Qty received" },
                Rows = batches.Select(b => new List<string>
                {
                    b.ProductId != null && names.TryGetValue(b.ProductId, out var n) ? n ?? "" : "",
                    b.BatchNumber ?? "", b.ExpiryDate!.Value.ToString("dd MMM yyyy"), N(b.Quantity ?? 0)
                }).ToList(),
                Suggestions = new() { "What is low on stock?", "Which products are already expired?" }
            };
        }

        // ---- low stock ----
        if (Has(q, "low", "reorder", "re-order", "shortage", "below", "need to buy", "need to order", "restock"))
        {
            var stock = await LoadStock(ct);
            var products = await LoadProducts(ct);
            var low = products.Where(p => p.ReorderPoint.HasValue && (stock.TryGetValue(p.Id, out var s) ? s : 0) <= p.ReorderPoint.Value)
                .Select(p => new { P = p, Stock = stock.TryGetValue(p.Id, out var s2) ? s2 : 0 })
                .OrderBy(x => x.Stock - x.P.ReorderPoint!.Value).Take(50).ToList();
            if (low.Count == 0) return Simple("Nothing is at or below its reorder point. (Products only count once a Reorder Point is set.)");
            return new AskInventoryResult
            {
                Answer = $"{low.Count} product(s) are at or below their reorder point. You can turn them into a draft purchase order from Purchase → Reorder Suggestions.",
                Columns = new() { "Product", "In stock", "Reorder point" },
                Rows = low.Select(x => new List<string> { x.P.Name ?? "", N(x.Stock), N(x.P.ReorderPoint!.Value) }).ToList(),
                Suggestions = new() { "What will run out in 2 weeks?", "Which products are out of stock?" }
            };
        }

        // ---- inventory value ----
        if (Has(q, "value", "worth", "valuation"))
        {
            var stock = await LoadStock(ct);
            var products = await LoadProducts(ct);
            var total = products.Sum(p => (stock.TryGetValue(p.Id, out var s) ? Math.Max(s, 0) : 0) * p.UnitPrice);
            var units = products.Sum(p => stock.TryGetValue(p.Id, out var s) ? Math.Max(s, 0) : 0);
            return Simple($"Total inventory value is {N(total)} across {N(units)} units (stock x unit price).");
        }

        // ---- counts ----
        if (Has(q, "how many products", "number of products", "total products", "how many skus", "count of products"))
        {
            var products = await LoadProducts(ct);
            return Simple($"You have {products.Count} physical products.");
        }

        // ---- stock of a specific product ----
        {
            var products = await LoadProducts(ct);
            var matches = MatchProducts(q, products);
            if (matches.Count > 0)
            {
                var stock = await LoadStock(ct);
                return new AskInventoryResult
                {
                    Answer = matches.Count == 1 ? "Here is the stock for that product:" : $"{matches.Count} products match:",
                    Columns = new() { "Product", "Number", "In stock", "Reorder point", "Unit price" },
                    Rows = matches.Take(15).Select(p => new List<string>
                    {
                        p.Name ?? "", p.Number ?? "",
                        N(stock.TryGetValue(p.Id, out var s) ? s : 0),
                        p.ReorderPoint.HasValue ? N(p.ReorderPoint.Value) : "-",
                        N(p.UnitPrice)
                    }).ToList(),
                    Suggestions = Examples.Take(3).ToList()
                };
            }
        }

        return Help("I didn't understand that one. I can answer questions like:");
    }

    private static List<P> MatchProducts(string q, List<P> products)
    {
        var contained = products.Where(p =>
            (!string.IsNullOrWhiteSpace(p.Name) && q.Contains(p.Name!.ToLowerInvariant())) ||
            (!string.IsNullOrWhiteSpace(p.Number) && q.Contains(p.Number!.ToLowerInvariant()))).ToList();
        if (contained.Count > 0) return contained;

        var tokens = Regex.Matches(q, @"[a-z0-9]{3,}").Select(m => m.Value).Where(t => !StopWords.Contains(t)).ToHashSet();
        if (tokens.Count == 0) return new();

        var scored = products
            .Select(p => new { P = p, Score = Regex.Matches((p.Name ?? "").ToLowerInvariant(), @"[a-z0-9]{3,}").Count(m => tokens.Contains(m.Value)) })
            .Where(x => x.Score > 0).ToList();
        if (scored.Count == 0) return new();
        var best = scored.Max(x => x.Score);
        return scored.Where(x => x.Score == best).Select(x => x.P).ToList();
    }

    private static AskInventoryResult Simple(string text) => new() { Answer = text, Suggestions = Examples.Take(3).ToList() };

    private static AskInventoryResult Help(string text) => new() { Answer = text, Suggestions = Examples.ToList() };
}
