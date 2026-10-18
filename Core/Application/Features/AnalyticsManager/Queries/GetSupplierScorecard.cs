using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AnalyticsManager.Queries;

public class SupplierScoreDto
{
    public string? VendorId { get; init; }
    public string? VendorName { get; init; }
    public int Orders { get; init; }
    public double Spend { get; init; }
    public double Ordered { get; init; }
    public double Received { get; init; }
    public double Returned { get; init; }
    public double Open { get; init; }
    /// <summary>Share of ordered units that arrived, 0 to 100.</summary>
    public double FillRate { get; init; }
    public double ReturnRate { get; init; }
    /// <summary>Average days from order date to receipt date.</summary>
    public double? LeadDays { get; init; }
    public double Score { get; init; }
    public string Grade { get; init; } = "-";
}

public class GetSupplierScorecardResult
{
    public List<SupplierScoreDto> Rows { get; init; } = new();
}

public class GetSupplierScorecardRequest : IRequest<GetSupplierScorecardResult>
{
    public int Days { get; init; } = 365;
}

public class GetSupplierScorecardHandler : IRequestHandler<GetSupplierScorecardRequest, GetSupplierScorecardResult>
{
    private readonly IQueryContext _context;

    public GetSupplierScorecardHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetSupplierScorecardResult> Handle(GetSupplierScorecardRequest r, CancellationToken ct)
    {
        var since = DateTime.UtcNow.Date.AddDays(-Math.Clamp(r.Days, 30, 1825));
        var pos = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(p => p.OrderDate >= since && (p.OrderStatus == PurchaseOrderStatus.Confirmed || p.OrderStatus == PurchaseOrderStatus.Archived))
            .Select(p => new { p.Id, p.VendorId, VendorName = p.Vendor!.Name, p.OrderDate, p.AfterTaxAmount }).ToListAsync(ct);
        if (pos.Count == 0) return new GetSupplierScorecardResult();
        var poIds = pos.Select(p => p.Id).ToList();

        var orderedRows = await _context.PurchaseOrderItem.AsNoTracking().ApplyIsDeletedFilter(false).Where(i => poIds.Contains(i.PurchaseOrderId!))
            .GroupBy(i => i.PurchaseOrderId).Select(g => new { Id = g.Key, Qty = g.Sum(i => i.Quantity) }).ToListAsync(ct);
        var orderedByPo = orderedRows.Where(x => x.Id != null).ToDictionary(x => x.Id!, x => x.Qty ?? 0.0);

        var grs = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(g => poIds.Contains(g.PurchaseOrderId!) && (g.Status == GoodsReceiveStatus.Confirmed || g.Status == GoodsReceiveStatus.Archived))
            .Select(g => new { g.Id, g.PurchaseOrderId, g.ReceiveDate }).ToListAsync(ct);
        var grIds = grs.Select(g => g.Id).ToList();
        var recvRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(GoodsReceive) && x.Status == InventoryTransactionStatus.Confirmed && grIds.Contains(x.ModuleId!))
            .GroupBy(x => x.ModuleId).Select(g => new { Id = g.Key, Qty = g.Sum(x => x.Movement) }).ToListAsync(ct);
        var recvByGr = recvRows.Where(x => x.Id != null).ToDictionary(x => x.Id!, x => Math.Abs(x.Qty ?? 0));

        var prs = await _context.PurchaseReturn.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(p => grIds.Contains(p.GoodsReceiveId!) && (p.Status == PurchaseReturnStatus.Confirmed || p.Status == PurchaseReturnStatus.Archived))
            .Select(p => new { p.Id, p.GoodsReceiveId }).ToListAsync(ct);
        var prIds = prs.Select(p => p.Id).ToList();
        var retRows = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(PurchaseReturn) && x.Status == InventoryTransactionStatus.Confirmed && prIds.Contains(x.ModuleId!))
            .GroupBy(x => x.ModuleId).Select(g => new { Id = g.Key, Qty = g.Sum(x => x.Movement) }).ToListAsync(ct);
        var retByPr = retRows.Where(x => x.Id != null).ToDictionary(x => x.Id!, x => Math.Abs(x.Qty ?? 0));

        var grToPo = grs.ToDictionary(g => g.Id, g => g.PurchaseOrderId);
        var prToPo = prs.Where(p => p.GoodsReceiveId != null && grToPo.ContainsKey(p.GoodsReceiveId)).ToDictionary(p => p.Id, p => grToPo[p.GoodsReceiveId!]);
        var orderDate = pos.ToDictionary(p => p.Id, p => p.OrderDate);

        var rows = new List<SupplierScoreDto>();
        foreach (var g in pos.GroupBy(p => p.VendorId))
        {
            var ids = g.Select(p => p.Id).ToHashSet();
            var ordered = g.Sum(p => orderedByPo.TryGetValue(p.Id, out var q) ? q : 0);
            var received = grs.Where(x => x.PurchaseOrderId != null && ids.Contains(x.PurchaseOrderId)).Sum(x => recvByGr.TryGetValue(x.Id, out var q) ? q : 0);
            var returned = prs.Where(x => prToPo.TryGetValue(x.Id, out var po) && po != null && ids.Contains(po)).Sum(x => retByPr.TryGetValue(x.Id, out var q) ? q : 0);
            var leads = grs.Where(x => x.PurchaseOrderId != null && ids.Contains(x.PurchaseOrderId) && x.ReceiveDate != null && orderDate[x.PurchaseOrderId] != null)
                .Select(x => (x.ReceiveDate!.Value.Date - orderDate[x.PurchaseOrderId!]!.Value.Date).TotalDays).Where(d => d >= 0).ToList();
            double? lead = leads.Count > 0 ? Math.Round(leads.Average(), 1) : null;

            var fill = ordered > 0 ? Math.Min(received / ordered, 1) : 0;
            var retRate = received > 0 ? Math.Min(returned / received, 1) : 0;
            var speed = lead == null ? 0.5 : lead <= 7 ? 1 : Math.Min(1, 7.0 / lead.Value);
            var score = Math.Round(100 * (0.5 * fill + 0.3 * (1 - retRate) + 0.2 * speed), 0);
            rows.Add(new SupplierScoreDto
            {
                VendorId = g.Key, VendorName = g.First().VendorName, Orders = g.Count(), Spend = g.Sum(p => p.AfterTaxAmount ?? 0), Ordered = ordered, Received = received, Returned = returned,
                Open = Math.Max(ordered - received, 0), FillRate = Math.Round(fill * 100, 1), ReturnRate = Math.Round(retRate * 100, 1), LeadDays = lead, Score = score,
                Grade = score >= 90 ? "A" : score >= 75 ? "B" : score >= 60 ? "C" : "D"
            });
        }
        return new GetSupplierScorecardResult { Rows = rows.OrderByDescending(x => x.Spend).ToList() };
    }
}
