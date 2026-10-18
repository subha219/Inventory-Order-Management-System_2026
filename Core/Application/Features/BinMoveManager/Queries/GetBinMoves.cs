using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.BinMoveManager.Queries;

public class BinStockDto
{
    public string? LocationId { get; init; }
    public string? LocationCode { get; init; }
    public string? Zone { get; init; }
    public string? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Quantity { get; init; }
}

public class BinLocationDto
{
    public string? Id { get; init; }
    public string? Code { get; init; }
    public string? Zone { get; init; }
    public string? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public double? Capacity { get; init; }
    public double Used { get; init; }
}

public class ReplenishRuleDto
{
    public string? Id { get; init; }
    public string? LocationId { get; init; }
    public string? LocationCode { get; init; }
    public string? ProductId { get; init; }
    public string? ProductName { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Current { get; init; }
}

public class ReplenishSuggestionDto
{
    public string? RuleId { get; init; }
    public string? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? ToLocationId { get; init; }
    public string? ToLocationCode { get; init; }
    public string? FromLocationId { get; init; }
    public string? FromLocationCode { get; init; }
    public double Current { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Quantity { get; init; }
    /// <summary>True when no other location holds enough stock to refill it.</summary>
    public bool Short { get; init; }
}

public class MoveHistoryDto
{
    public DateTime? At { get; init; }
    public string? ProductName { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public double Quantity { get; init; }
    public string? MoveType { get; init; }
    public string? Reason { get; init; }
    public string? UserId { get; init; }
}

public class GetBinMovesResult
{
    public List<BinStockDto> Stock { get; init; } = new();
    public List<BinLocationDto> Locations { get; init; } = new();
    public List<ReplenishRuleDto> Rules { get; init; } = new();
    public List<ReplenishSuggestionDto> Suggestions { get; init; } = new();
    public List<MoveHistoryDto> History { get; init; } = new();
}

public class GetBinMovesRequest : IRequest<GetBinMovesResult>
{
}

public class GetBinMovesHandler : IRequestHandler<GetBinMovesRequest, GetBinMovesResult>
{
    private readonly IQueryContext _context;

    public GetBinMovesHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetBinMovesResult> Handle(GetBinMovesRequest request, CancellationToken ct)
    {
        var locs = await _context.StorageLocation.AsNoTracking().ApplyIsDeletedFilter(false).Where(l => l.IsActive)
            .Select(l => new { l.Id, l.Code, l.Zone, l.WarehouseId, Warehouse = l.Warehouse!.Name, l.Capacity }).ToListAsync(ct);
        var stockRows = await _context.LocationStock.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => (s.Quantity ?? 0) > 0)
            .Select(s => new { s.LocationId, s.ProductId, Qty = s.Quantity ?? 0 }).ToListAsync(ct);
        var rules = await _context.ReplenishmentRule.AsNoTracking().ApplyIsDeletedFilter(false).ToListAsync(ct);
        var moves = await _context.LocationMove.AsNoTracking().ApplyIsDeletedFilter(false).OrderByDescending(m => m.CreatedAtUtc).Take(60).ToListAsync(ct);

        var productIds = stockRows.Select(s => s.ProductId).Concat(rules.Select(r => r.ProductId)).Concat(moves.Select(m => m.ProductId)).Distinct().ToList();
        var products = await _context.Product.AsNoTracking().Where(p => productIds.Contains(p.Id)).Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);
        string PName(string? id) => products.FirstOrDefault(p => p.Id == id)?.Name ?? "";
        var locById = locs.ToDictionary(l => l.Id);
        string LCode(string? id) => id != null && locById.TryGetValue(id, out var l) ? (l.Code ?? "") : "";

        var usedBy = stockRows.GroupBy(s => s.LocationId ?? "").ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
        double Current(string? loc, string? pid) => stockRows.Where(s => s.LocationId == loc && s.ProductId == pid).Sum(s => s.Qty);

        var result = new GetBinMovesResult
        {
            Locations = locs.OrderBy(l => l.Warehouse).ThenBy(l => l.Zone).ThenBy(l => l.Code).Select(l => new BinLocationDto
            {
                Id = l.Id, Code = l.Code, Zone = l.Zone, WarehouseId = l.WarehouseId, WarehouseName = l.Warehouse, Capacity = l.Capacity,
                Used = usedBy.TryGetValue(l.Id, out var u) ? u : 0
            }).ToList(),
            Stock = stockRows.Where(s => s.LocationId != null && locById.ContainsKey(s.LocationId)).Select(s =>
            {
                var l = locById[s.LocationId!];
                var p = products.FirstOrDefault(x => x.Id == s.ProductId);
                return new BinStockDto
                {
                    LocationId = l.Id, LocationCode = l.Code, Zone = l.Zone, WarehouseId = l.WarehouseId, WarehouseName = l.Warehouse,
                    ProductId = s.ProductId, ProductNumber = p?.Number, ProductName = p?.Name, Quantity = s.Qty
                };
            }).OrderBy(s => s.ProductName).ThenBy(s => s.LocationCode).ToList(),
            Rules = rules.Where(r => r.LocationId != null && locById.ContainsKey(r.LocationId)).Select(r => new ReplenishRuleDto
            {
                Id = r.Id, LocationId = r.LocationId, LocationCode = LCode(r.LocationId), ProductId = r.ProductId, ProductName = PName(r.ProductId),
                Min = r.MinQuantity ?? 0, Max = r.MaxQuantity ?? 0, Current = Current(r.LocationId, r.ProductId)
            }).OrderBy(r => r.LocationCode).ToList(),
            History = moves.Select(m => new MoveHistoryDto
            {
                At = m.CreatedAtUtc, ProductName = PName(m.ProductId), From = LCode(m.FromLocationId), To = LCode(m.ToLocationId),
                Quantity = m.Quantity ?? 0, MoveType = m.MoveType, Reason = m.Reason, UserId = m.CreatedById
            }).ToList()
        };

        // suggestions: below Min -> refill up to Max from the location (same warehouse) with the most of that product
        var suggestions = new List<ReplenishSuggestionDto>();
        var taken = new Dictionary<string, double>(); // stock already promised by an earlier suggestion
        foreach (var r in rules.Where(r => r.LocationId != null && locById.ContainsKey(r.LocationId!) && (r.MaxQuantity ?? 0) > 0))
        {
            var cur = Current(r.LocationId, r.ProductId);
            if (cur >= (r.MinQuantity ?? 0) + 1e-9 || cur >= (r.MaxQuantity ?? 0)) continue;
            var target = locById[r.LocationId!];
            var need = (r.MaxQuantity ?? 0) - cur;
            if (target.Capacity.HasValue) need = Math.Min(need, Math.Max(target.Capacity.Value - (usedBy.TryGetValue(target.Id, out var uu) ? uu : 0), 0));
            if (need <= 1e-9) continue;
            var source = stockRows.Where(s => s.ProductId == r.ProductId && s.LocationId != r.LocationId && s.LocationId != null && locById.ContainsKey(s.LocationId) &&
                                              locById[s.LocationId].WarehouseId == target.WarehouseId)
                .Select(s => new { s.LocationId, Free = s.Qty - (taken.TryGetValue(s.LocationId! + "|" + s.ProductId, out var t) ? t : 0) })
                .Where(s => s.Free > 1e-9).OrderByDescending(s => s.Free).FirstOrDefault();
            var qty = source == null ? 0 : Math.Min(need, source.Free);
            if (source != null) taken[source.LocationId! + "|" + r.ProductId] = (taken.TryGetValue(source.LocationId! + "|" + r.ProductId, out var t0) ? t0 : 0) + qty;
            suggestions.Add(new ReplenishSuggestionDto
            {
                RuleId = r.Id, ProductId = r.ProductId, ProductName = PName(r.ProductId), ToLocationId = target.Id, ToLocationCode = target.Code,
                FromLocationId = source?.LocationId, FromLocationCode = LCode(source?.LocationId), Current = cur, Min = r.MinQuantity ?? 0, Max = r.MaxQuantity ?? 0,
                Quantity = qty, Short = source == null || qty + 1e-9 < need
            });
        }
        return new GetBinMovesResult { Locations = result.Locations, Stock = result.Stock, Rules = result.Rules, History = result.History, Suggestions = suggestions };
    }
}
