using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StockHoldManager.Queries;

public class StockHoldDto
{
    public string? Id { get; init; }
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public double Quantity { get; init; }
    public double OriginalQuantity { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public string? Status { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public string? CreatedById { get; init; }
    public DateTime? ReleasedAtUtc { get; init; }
}

public class HoldStockOptionDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? WarehouseId { get; init; }
    public double OnHand { get; init; }
    public double Held { get; init; }
}

public class HoldWarehouseDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class GetStockHoldsResult
{
    public List<StockHoldDto> Holds { get; init; } = new();
    public List<HoldStockOptionDto> Stock { get; init; } = new();
    public List<HoldWarehouseDto> Warehouses { get; init; } = new();
}

public class GetStockHoldsRequest : IRequest<GetStockHoldsResult>
{
}

public class GetStockHoldsHandler : IRequestHandler<GetStockHoldsRequest, GetStockHoldsResult>
{
    private readonly IQueryContext _context;

    public GetStockHoldsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetStockHoldsResult> Handle(GetStockHoldsRequest request, CancellationToken ct)
    {
        var since = DateTime.UtcNow.Date.AddDays(-60);
        var holds = await _context.StockHold.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(h => h.Status == "Active" || h.ReleasedAtUtc >= since).OrderBy(h => h.Status == "Active" ? 0 : 1).ThenByDescending(h => h.CreatedAtUtc).Take(300).ToListAsync(ct);
        var warehouses = await _context.Warehouse.AsNoTracking().ApplyIsDeletedFilter(false).Where(w => w.SystemWarehouse == false).OrderBy(w => w.Name)
            .Select(w => new HoldWarehouseDto { Id = w.Id, Name = w.Name }).ToListAsync(ct);
        var whName = warehouses.ToDictionary(w => w.Id ?? "", w => w.Name);

        var stockRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false && x.Product!.Physical == true)
            .GroupBy(x => new { x.ProductId, x.WarehouseId }).Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Stock = g.Sum(x => x.Stock) }).ToListAsync(ct);
        stockRows = stockRows.Where(s => (s.Stock ?? 0) > 0).Take(5000).ToList();

        var ids = stockRows.Select(s => s.ProductId).Concat(holds.Select(h => h.ProductId)).Distinct().ToList();
        var prods = await _context.Product.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);
        var pd = prods.ToDictionary(p => p.Id);
        var activeHeld = holds.Where(h => h.Status == "Active").GroupBy(h => (h.ProductId, h.WarehouseId)).ToDictionary(g => g.Key, g => g.Sum(h => h.Quantity ?? 0));

        return new GetStockHoldsResult
        {
            Warehouses = warehouses,
            Holds = holds.Select(h => new StockHoldDto
            {
                Id = h.Id, ProductId = h.ProductId, ProductNumber = pd.TryGetValue(h.ProductId ?? "", out var p) ? p.Number : "", ProductName = p?.Name,
                WarehouseId = h.WarehouseId, WarehouseName = whName.TryGetValue(h.WarehouseId ?? "", out var w) ? w : "", Quantity = h.Quantity ?? 0,
                OriginalQuantity = h.OriginalQuantity ?? 0, Reason = h.Reason, Notes = h.Notes, Status = h.Status, CreatedAtUtc = h.CreatedAtUtc, CreatedById = h.CreatedById, ReleasedAtUtc = h.ReleasedAtUtc
            }).ToList(),
            Stock = stockRows.Where(s => s.ProductId != null && pd.ContainsKey(s.ProductId)).Select(s => new HoldStockOptionDto
            {
                ProductId = s.ProductId, ProductNumber = pd[s.ProductId!].Number, ProductName = pd[s.ProductId!].Name, WarehouseId = s.WarehouseId, OnHand = s.Stock ?? 0,
                Held = activeHeld.TryGetValue((s.ProductId, s.WarehouseId), out var hq) ? hq : 0
            }).OrderBy(s => s.ProductName).ToList()
        };
    }
}
