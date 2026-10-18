using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AnalyticsManager.Queries;

public class KpiDto
{
    public string? Key { get; init; }
    public string? Label { get; init; }
    public double? Value { get; init; }
    public string? Unit { get; init; }
    public string? Note { get; init; }
    /// <summary>good, warn, bad or empty for no judgement.</summary>
    public string? Tone { get; init; }
}

public class KpiDayDto
{
    public string Date { get; init; } = string.Empty;
    public int Shipments { get; init; }
    public int Receipts { get; init; }
}

public class GetOperationsKpisResult
{
    public List<KpiDto> Kpis { get; init; } = new();
    public List<KpiDayDto> Days { get; init; } = new();
}

public class GetOperationsKpisRequest : IRequest<GetOperationsKpisResult>
{
    public int Days { get; init; } = 30;
}

public class GetOperationsKpisHandler : IRequestHandler<GetOperationsKpisRequest, GetOperationsKpisResult>
{
    private readonly IQueryContext _context;

    public GetOperationsKpisHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetOperationsKpisResult> Handle(GetOperationsKpisRequest r, CancellationToken ct)
    {
        var days = Math.Clamp(r.Days, 7, 365);
        var today = DateTime.UtcNow.Date;
        var since = today.AddDays(-days + 1);
        var kpis = new List<KpiDto>();
        static string Tone(double v, double good, double warn, bool higherBetter = true) =>
            higherBetter ? (v >= good ? "good" : v >= warn ? "warn" : "bad") : (v <= good ? "good" : v <= warn ? "warn" : "bad");

        // ---- shipping
        var shipped = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => s.Status == "Shipped" && s.ShippedAtUtc >= since)
            .Select(s => new { s.Id, s.CreatedAtUtc, s.ShippedAtUtc, s.DeliveryOrderId }).ToListAsync(ct);
        var packHours = shipped.Where(s => s.CreatedAtUtc != null && s.ShippedAtUtc != null).Select(s => (s.ShippedAtUtc!.Value - s.CreatedAtUtc!.Value).TotalHours).ToList();
        kpis.Add(new KpiDto { Key = "shipments", Label = "Shipments sent", Value = shipped.Count, Unit = "", Note = "Shipped in the period" });
        kpis.Add(new KpiDto { Key = "packToShip", Label = "Pack-to-ship time", Value = packHours.Count > 0 ? Math.Round(packHours.Average(), 1) : null, Unit = "hours", Note = "From starting to pack to handing over", Tone = packHours.Count > 0 ? Tone(packHours.Average(), 8, 24, false) : null });

        // order cycle: sales order date to the first shipment leaving
        var doIds = shipped.Select(s => s.DeliveryOrderId).Distinct().ToList();
        var doSo = await _context.DeliveryOrder.AsNoTracking().Where(d => doIds.Contains(d.Id)).Select(d => new { d.Id, d.SalesOrderId, OrderDate = d.SalesOrder!.OrderDate }).ToListAsync(ct);
        var soDate = doSo.Where(d => d.OrderDate != null).ToDictionary(d => d.Id, d => d.OrderDate!.Value);
        var cycle = shipped.Where(s => s.DeliveryOrderId != null && soDate.ContainsKey(s.DeliveryOrderId) && s.ShippedAtUtc != null)
            .Select(s => (s.ShippedAtUtc!.Value.Date - soDate[s.DeliveryOrderId!].Date).TotalDays).Where(d => d >= 0).ToList();
        kpis.Add(new KpiDto { Key = "orderCycle", Label = "Order cycle time", Value = cycle.Count > 0 ? Math.Round(cycle.Average(), 1) : null, Unit = "days", Note = "Sales order date to shipment", Tone = cycle.Count > 0 ? Tone(cycle.Average(), 2, 5, false) : null });

        // ---- receiving
        var grs = await _context.GoodsReceive.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(g => (g.Status == GoodsReceiveStatus.Confirmed || g.Status == GoodsReceiveStatus.Archived) && g.ReceiveDate >= since)
            .Select(g => new { g.Id, g.ReceiveDate, PoDate = g.PurchaseOrder!.OrderDate }).ToListAsync(ct);
        var lead = grs.Where(g => g.ReceiveDate != null && g.PoDate != null).Select(g => (g.ReceiveDate!.Value.Date - g.PoDate!.Value.Date).TotalDays).Where(d => d >= 0).ToList();
        kpis.Add(new KpiDto { Key = "receipts", Label = "Goods receipts", Value = grs.Count, Unit = "", Note = "Confirmed in the period" });
        kpis.Add(new KpiDto { Key = "lead", Label = "Supplier lead time", Value = lead.Count > 0 ? Math.Round(lead.Average(), 1) : null, Unit = "days", Note = "Purchase order date to receipt", Tone = lead.Count > 0 ? Tone(lead.Average(), 7, 14, false) : null });

        // ---- dock
        var appts = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false).Where(a => a.StartAtUtc >= since && a.StartAtUtc < today.AddDays(1)).ToListAsync(ct);
        var closed = appts.Where(a => a.Status == "Completed" || a.Status == "NoShow" || a.Status == "CheckedIn").ToList();
        var arrived = appts.Where(a => a.CheckedInAtUtc != null).ToList();
        var onTime = arrived.Count > 0 ? 100.0 * arrived.Count(a => a.CheckedInAtUtc <= a.StartAtUtc!.Value.AddMinutes(15)) / arrived.Count : (double?)null;
        var noShow = closed.Count > 0 ? 100.0 * appts.Count(a => a.Status == "NoShow") / closed.Count : (double?)null;
        var dwell = appts.Where(a => a.Status == "Completed" && a.CheckedInAtUtc != null && a.UpdatedAtUtc != null).Select(a => (a.UpdatedAtUtc!.Value - a.CheckedInAtUtc!.Value).TotalMinutes).Where(m => m >= 0).ToList();
        kpis.Add(new KpiDto { Key = "dockOnTime", Label = "Trucks on time", Value = onTime == null ? null : Math.Round(onTime.Value, 0), Unit = "%", Note = "Arrived within 15 minutes of the slot", Tone = onTime == null ? null : Tone(onTime.Value, 90, 75) });
        kpis.Add(new KpiDto { Key = "noShow", Label = "Dock no-shows", Value = noShow == null ? null : Math.Round(noShow.Value, 0), Unit = "%", Note = "Booked trucks that never came", Tone = noShow == null ? null : Tone(noShow.Value, 5, 15, false) });
        kpis.Add(new KpiDto { Key = "dwell", Label = "Truck turnaround", Value = dwell.Count > 0 ? Math.Round(dwell.Average(), 0) : null, Unit = "min", Note = "Check-in to completed" });

        // ---- returns and accuracy
        var moves = _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false).Where(x => x.Status == InventoryTransactionStatus.Confirmed && x.MovementDate >= since);
        var outQty = Math.Abs(await moves.Where(x => x.ModuleName == nameof(DeliveryOrder)).SumAsync(x => (double?)x.Movement, ct) ?? 0);
        var retQty = Math.Abs(await moves.Where(x => x.ModuleName == nameof(SalesReturn)).SumAsync(x => (double?)x.Movement, ct) ?? 0);
        kpis.Add(new KpiDto { Key = "returnRate", Label = "Customer return rate", Value = outQty > 0 ? Math.Round(100 * retQty / outQty, 1) : null, Unit = "%", Note = "Units returned against units delivered", Tone = outQty > 0 ? Tone(100 * retQty / outQty, 2, 5, false) : null });

        var counts = await moves.Where(x => x.ModuleName == nameof(StockCount)).Select(x => new { Sys = x.QtySCSys, Delta = x.QtySCDelta }).ToListAsync(ct);
        var sys = counts.Sum(c => Math.Abs(c.Sys ?? 0)); var delta = counts.Sum(c => Math.Abs(c.Delta ?? 0));
        kpis.Add(new KpiDto { Key = "accuracy", Label = "Inventory accuracy", Value = sys > 0 ? Math.Round(100 * (1 - Math.Min(delta / sys, 1)), 1) : null, Unit = "%", Note = "From stock counts: counted against system quantity", Tone = sys > 0 ? Tone(100 * (1 - Math.Min(delta / sys, 1)), 98, 95) : null });

        // ---- open work right now
        var openWaves = await _context.PickWave.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(w => w.Status == "Open", ct);
        var packing = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(s => s.Status == "Packing", ct);
        var lateNow = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false).CountAsync(a => a.Status == "Scheduled" && a.StartAtUtc < DateTime.UtcNow.AddMinutes(-15), ct);
        kpis.Add(new KpiDto { Key = "openWaves", Label = "Open pick waves", Value = openWaves, Unit = "", Note = "Waiting to be picked" });
        kpis.Add(new KpiDto { Key = "packing", Label = "Shipments being packed", Value = packing, Unit = "", Note = "Not yet handed over" });
        kpis.Add(new KpiDto { Key = "lateNow", Label = "Trucks late right now", Value = lateNow, Unit = "", Note = "Scheduled, over 15 minutes past slot", Tone = lateNow == 0 ? "good" : "bad" });

        var series = new List<KpiDayDto>();
        for (var d = since; d <= today; d = d.AddDays(1))
            series.Add(new KpiDayDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                Shipments = shipped.Count(s => s.ShippedAtUtc!.Value.Date == d),
                Receipts = grs.Count(g => g.ReceiveDate!.Value.Date == d)
            });
        return new GetOperationsKpisResult { Kpis = kpis, Days = series };
    }
}
