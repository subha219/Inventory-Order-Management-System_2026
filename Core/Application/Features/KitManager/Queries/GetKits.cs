using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.KitManager.Queries;

public class KitComponentDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double PerKit { get; init; }
    public double OnHand { get; init; }
    /// <summary>How many kits this component alone allows.</summary>
    public double Allows { get; init; }
}

public class KitDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double OnHand { get; init; }
    public double Buildable { get; init; }
    public string? Limiting { get; init; }
    public List<KitComponentDto> Components { get; init; } = new();
}

public class KitProductOptionDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Name { get; init; }
}

public class KitWarehouseDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class KitHistoryDto
{
    public DateTime? At { get; init; }
    public string? Number { get; init; }
    public string? KitName { get; init; }
    public double Quantity { get; init; }
    public string? WarehouseName { get; init; }
    public string? ComponentsDocument { get; init; }
    public string? KitsDocument { get; init; }
    public string? UserId { get; init; }
}

public class GetKitsResult
{
    public List<KitDto> Kits { get; init; } = new();
    public List<KitProductOptionDto> Products { get; init; } = new();
    public List<KitWarehouseDto> Warehouses { get; init; } = new();
    public List<KitHistoryDto> History { get; init; } = new();
}

public class GetKitsRequest : IRequest<GetKitsResult>
{
    /// <summary>Count stock in this warehouse; empty means all warehouses together.</summary>
    public string? WarehouseId { get; init; }
}

public class GetKitsHandler : IRequestHandler<GetKitsRequest, GetKitsResult>
{
    private readonly IQueryContext _context;

    public GetKitsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetKitsResult> Handle(GetKitsRequest request, CancellationToken ct)
    {
        var lines = await _context.ProductBomLine.AsNoTracking().ApplyIsDeletedFilter(false).ToListAsync(ct);
        var products = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.Physical == true)
            .Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);
        var warehouses = await _context.Warehouse.AsNoTracking().ApplyIsDeletedFilter(false).Where(w => w.SystemWarehouse == false)
            .OrderBy(w => w.Name).Select(w => new KitWarehouseDto { Id = w.Id, Name = w.Name }).ToListAsync(ct);

        var onHandQ = _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false);
        if (!string.IsNullOrEmpty(request.WarehouseId)) onHandQ = onHandQ.Where(x => x.WarehouseId == request.WarehouseId);
        var onHandRows = await onHandQ.GroupBy(x => x.ProductId).Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) }).ToListAsync(ct);
        var onHand = onHandRows.ToDictionary(r => r.ProductId ?? "", r => r.Stock ?? 0.0);
        double Have(string? id) => onHand.TryGetValue(id ?? "", out var v) ? v : 0;
        var byId = products.ToDictionary(p => p.Id);

        var kits = new List<KitDto>();
        foreach (var g in lines.GroupBy(l => l.KitProductId))
        {
            if (g.Key == null || !byId.TryGetValue(g.Key, out var kit)) continue;
            var comps = g.Where(l => l.ComponentProductId != null && byId.ContainsKey(l.ComponentProductId)).Select(l =>
            {
                var c = byId[l.ComponentProductId!];
                var per = l.Quantity ?? 0;
                return new KitComponentDto
                {
                    ProductId = c.Id, ProductNumber = c.Number, ProductName = c.Name, PerKit = per, OnHand = Have(c.Id),
                    Allows = per > 0 ? Math.Floor(Math.Max(Have(c.Id), 0) / per) : 0
                };
            }).ToList();
            var limiting = comps.OrderBy(c => c.Allows).FirstOrDefault();
            kits.Add(new KitDto
            {
                ProductId = kit.Id, ProductNumber = kit.Number, ProductName = kit.Name, OnHand = Have(kit.Id), Components = comps,
                Buildable = comps.Count == 0 ? 0 : comps.Min(c => c.Allows), Limiting = comps.Count > 1 ? limiting?.ProductName : null
            });
        }

        var asm = await _context.KitAssembly.AsNoTracking().ApplyIsDeletedFilter(false).OrderByDescending(a => a.CreatedAtUtc).Take(30).ToListAsync(ct);
        var whNames = warehouses.ToDictionary(w => w.Id ?? "", w => w.Name);
        return new GetKitsResult
        {
            Kits = kits.OrderBy(k => k.ProductName).ToList(),
            Warehouses = warehouses,
            Products = products.OrderBy(p => p.Name).Select(p => new KitProductOptionDto { Id = p.Id, Number = p.Number, Name = p.Name }).ToList(),
            History = asm.Select(a => new KitHistoryDto
            {
                At = a.CreatedAtUtc, Number = a.Number, KitName = byId.TryGetValue(a.KitProductId ?? "", out var kp) ? kp.Name : "", Quantity = a.Quantity ?? 0,
                WarehouseName = whNames.TryGetValue(a.WarehouseId ?? "", out var wn) ? wn : "", ComponentsDocument = a.ComponentsDocument, KitsDocument = a.KitsDocument, UserId = a.CreatedById
            }).ToList()
        };
    }
}
