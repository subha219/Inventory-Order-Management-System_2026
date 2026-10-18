using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CostingManager.Queries;

public class CostRowDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public string? GroupName { get; init; }
    public double OnHand { get; init; }
    public double AverageCost { get; init; }
    public double AverageValue { get; init; }
    public double FifoCost { get; init; }
    public double FifoValue { get; init; }
    public double SellingPrice { get; init; }
    /// <summary>Selling price minus average cost, as a share of the selling price. Null when no price is set.</summary>
    public double? MarginPercent { get; init; }
    public bool Estimated { get; init; }
}

public class LandedCostLineDto
{
    public string? Id { get; init; }
    public string? Description { get; init; }
    public double Amount { get; init; }
    public string? Basis { get; init; }
}

public class CostReceiptDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public DateTime? ReceiveDate { get; init; }
    public string? Vendor { get; init; }
    public double GoodsValue { get; init; }
    public double LandedTotal { get; init; }
    public List<LandedCostLineDto> Costs { get; init; } = new();
}

public class GetCostingResult
{
    public List<CostRowDto> Rows { get; init; } = new();
    public double TotalAverageValue { get; init; }
    public double TotalFifoValue { get; init; }
    public List<CostReceiptDto> Receipts { get; init; } = new();
}

public class GetCostingRequest : IRequest<GetCostingResult>
{
}

public class GetCostingHandler : IRequestHandler<GetCostingRequest, GetCostingResult>
{
    private readonly IQueryContext _context;

    public GetCostingHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetCostingResult> Handle(GetCostingRequest request, CancellationToken ct)
    {
        var receipts = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(g => g.Status == GoodsReceiveStatus.Confirmed)
            .Select(g => new { g.Id, g.Number, g.ReceiveDate, g.PurchaseOrderId, Vendor = g.PurchaseOrder!.Vendor!.Name })
            .ToListAsync(ct);
        var receiptIds = receipts.Select(r => r.Id).ToList();
        var poIds = receipts.Select(r => r.PurchaseOrderId).Distinct().ToList();

        var poItems = await _context.PurchaseOrderItem.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(i => poIds.Contains(i.PurchaseOrderId)).Select(i => new { i.PurchaseOrderId, i.ProductId, Price = i.UnitPrice ?? 0 }).ToListAsync(ct);
        var receiptLines = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(Domain.Entities.GoodsReceive) && receiptIds.Contains(x.ModuleId))
            .Select(x => new { x.ModuleId, x.ProductId, Qty = Math.Abs(x.Movement ?? 0) }).ToListAsync(ct);
        var landed = await _context.LandedCost.AsNoTracking().ApplyIsDeletedFilter(false).ToListAsync(ct);

        // unit cost per (receipt, product) = purchase price + share of landed costs
        var unitCost = new Dictionary<string, double>();
        foreach (var r in receipts)
        {
            var lines = receiptLines.Where(l => l.ModuleId == r.Id).GroupBy(l => l.ProductId)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Qty), Price = poItems.FirstOrDefault(i => i.PurchaseOrderId == r.PurchaseOrderId && i.ProductId == g.Key)?.Price ?? 0 }).ToList();
            var sumValue = lines.Sum(l => l.Qty * l.Price);
            var sumQty = lines.Sum(l => l.Qty);
            var costs = landed.Where(c => c.GoodsReceiveId == r.Id).ToList();
            foreach (var l in lines)
            {
                double extra = 0;
                foreach (var c in costs)
                {
                    var share = c.Basis == "Quantity" ? (sumQty > 0 ? l.Qty / sumQty : 0) : (sumValue > 0 ? l.Qty * l.Price / sumValue : (sumQty > 0 ? l.Qty / sumQty : 0));
                    extra += share * (c.Amount ?? 0);
                }
                unitCost[(r.Id ?? "") + "|" + l.ProductId] = l.Price + (l.Qty > 0 ? extra / l.Qty : 0);
            }
        }

        var moves = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.Warehouse!.SystemWarehouse == false && x.ProductId != null)
            .Select(x => new CostMove { ProductId = x.ProductId!, When = x.MovementDate ?? x.CreatedAtUtc ?? DateTime.MinValue, Qty = x.Stock ?? 0, ModuleName = x.ModuleName, ModuleId = x.ModuleId })
            .ToListAsync(ct);
        var products = await _context.Product.AsNoTracking().ApplyIsDeletedFilter(false).Where(p => p.Physical == true)
            .Select(p => new { p.Id, p.Number, p.Name, Group = p.ProductGroup!.Name, Price = p.UnitPrice ?? 0 }).ToListAsync(ct);

        var byProduct = moves.GroupBy(m => m.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        var rows = new List<CostRowDto>();
        foreach (var p in products)
        {
            if (!byProduct.TryGetValue(p.Id, out var list)) continue;
            var c = CostEngine.Calculate(list, (rid, pid) => unitCost.TryGetValue((rid ?? "") + "|" + pid, out var v) ? v : (double?)null, p.Price);
            if (Math.Abs(c.OnHand) < 1e-9) continue;
            rows.Add(new CostRowDto
            {
                ProductId = p.Id, ProductNumber = p.Number, ProductName = p.Name, GroupName = p.Group, OnHand = c.OnHand,
                AverageCost = c.AverageCost, AverageValue = c.AverageValue, FifoCost = c.FifoCost, FifoValue = c.FifoValue, SellingPrice = p.Price,
                MarginPercent = p.Price > 0 ? (p.Price - c.AverageCost) / p.Price * 100 : null, Estimated = c.Estimated
            });
        }

        var receiptDtos = receipts.OrderByDescending(r => r.ReceiveDate).Take(200).Select(r =>
        {
            var costs = landed.Where(c => c.GoodsReceiveId == r.Id).ToList();
            return new CostReceiptDto
            {
                Id = r.Id, Number = r.Number, ReceiveDate = r.ReceiveDate, Vendor = r.Vendor,
                GoodsValue = receiptLines.Where(l => l.ModuleId == r.Id).Sum(l => l.Qty * (poItems.FirstOrDefault(i => i.PurchaseOrderId == r.PurchaseOrderId && i.ProductId == l.ProductId)?.Price ?? 0)),
                LandedTotal = costs.Sum(c => c.Amount ?? 0),
                Costs = costs.Select(c => new LandedCostLineDto { Id = c.Id, Description = c.Description, Amount = c.Amount ?? 0, Basis = c.Basis }).ToList()
            };
        }).ToList();

        return new GetCostingResult
        {
            Rows = rows.OrderByDescending(r => r.AverageValue).ToList(),
            TotalAverageValue = rows.Sum(r => r.AverageValue),
            TotalFifoValue = rows.Sum(r => r.FifoValue),
            Receipts = receiptDtos
        };
    }
}
