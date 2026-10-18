using Application.Features.PickListManager.Queries;
using MediatR;

namespace Application.Features.WaveManager;

public class WaveRowDto
{
    public string? Zone { get; init; }
    public string? LocationId { get; init; }
    public string? Code { get; init; }
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Quantity { get; init; }
    public double InStock { get; init; }
    public List<WaveAllocDto> Allocations { get; init; } = new();
}

public class WaveAllocDto
{
    public string? OrderId { get; init; }
    public string? OrderNumber { get; init; }
    public double Quantity { get; init; }
}

public class WaveShortDto
{
    public string? OrderNumber { get; init; }
    public string? ProductName { get; init; }
    public double Short { get; init; }
}

public class WaveOrderDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Customer { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public double Needed { get; init; }
    public double Picked { get; init; }
}

/// <summary>One delivery order of a wave with its pick lines, loaded through the normal pick list query.</summary>
public class WaveOrderData
{
    public PickOrderDto Order { get; init; } = new();
    public List<PickLineDto> Lines { get; init; } = new();
}

public class WaveAllocation
{
    public List<WaveRowDto> Rows { get; init; } = new();
    public List<WaveShortDto> Shortfalls { get; init; } = new();
}

public static class WaveAllocator
{
    /// <summary>Loads each order's pick lines; orders come back oldest delivery date first.</summary>
    public static async Task<List<WaveOrderData>> Load(ISender sender, IEnumerable<string> orderIds, CancellationToken ct)
    {
        var list = new List<WaveOrderData>();
        foreach (var id in orderIds.Distinct())
        {
            var r = await sender.Send(new GetPickListRequest { DeliveryOrderId = id }, ct);
            var order = r.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) continue;   // not open any more
            list.Add(new WaveOrderData { Order = order, Lines = r.Lines });
        }
        return list.OrderBy(o => o.Order.DeliveryDate).ThenBy(o => o.Order.Number).ToList();
    }

    /// <summary>
    /// Shares the stock in each location between the orders, oldest order first, zone by zone, so the combined
    /// walk visits each location once. What no location can cover is returned as a shortfall.
    /// </summary>
    public static WaveAllocation Allocate(List<WaveOrderData> orders)
    {
        var avail = new Dictionary<string, double>();
        var rows = new Dictionary<string, (WaveRowDto Template, double Qty, List<WaveAllocDto> Allocs)>();
        var shorts = new List<WaveShortDto>();

        foreach (var o in orders)
        {
            foreach (var line in o.Lines.Where(l => l.Remaining > 1e-9))
            {
                var left = line.Remaining;
                foreach (var s in line.Sources.OrderBy(s => s.Zone ?? "~").ThenBy(s => s.Code))
                {
                    if (left <= 1e-9) break;
                    var key = line.ProductId + "|" + s.LocationId;
                    if (!avail.TryGetValue(key, out var have)) have = s.Quantity;
                    var take = Math.Min(left, have);
                    if (take <= 1e-9) { avail[key] = have; continue; }
                    avail[key] = have - take;
                    left -= take;

                    if (!rows.TryGetValue(key, out var row))
                    {
                        row = (new WaveRowDto { Zone = s.Zone, LocationId = s.LocationId, Code = s.Code, ProductId = line.ProductId, ProductNumber = line.ProductNumber, ProductName = line.ProductName, InStock = s.Quantity }, 0, new List<WaveAllocDto>());
                    }
                    row.Allocs.Add(new WaveAllocDto { OrderId = o.Order.Id, OrderNumber = o.Order.Number, Quantity = take });
                    rows[key] = (row.Template, row.Qty + take, row.Allocs);
                }
                if (left > 1e-9) shorts.Add(new WaveShortDto { OrderNumber = o.Order.Number, ProductName = line.ProductName, Short = left });
            }
        }

        var result = rows.Values.Select(r => new WaveRowDto
        {
            Zone = r.Template.Zone, LocationId = r.Template.LocationId, Code = r.Template.Code, ProductId = r.Template.ProductId,
            ProductNumber = r.Template.ProductNumber, ProductName = r.Template.ProductName, InStock = r.Template.InStock, Quantity = r.Qty, Allocations = r.Allocs
        }).OrderBy(r => r.Zone == null).ThenBy(r => r.Zone).ThenBy(r => r.Code).ThenBy(r => r.ProductName).ToList();
        return new WaveAllocation { Rows = result, Shortfalls = shorts };
    }
}
