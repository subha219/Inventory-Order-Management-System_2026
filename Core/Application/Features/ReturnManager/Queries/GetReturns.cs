using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReturnManager.Queries;

public class ReturnHeaderDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public DateTime? ReturnDate { get; init; }
    public string? Customer { get; init; }
    public double Units { get; init; }
    public bool Inspected { get; init; }
    public double Damaged { get; init; }
}

public class ReturnLineDto
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Returned { get; init; }
    public bool Inspected { get; init; }
    public double Good { get; init; }
    public double Damaged { get; init; }
    public string? ReasonCode { get; init; }
    public string? Note { get; init; }
    public string? LocationId { get; init; }
}

public class ReturnLocationDto
{
    public string? Id { get; init; }
    public string? Code { get; init; }
    public string? Zone { get; init; }
}

public class ReasonCountDto
{
    public string? Reason { get; init; }
    public double Units { get; init; }
}

public class GetReturnsResult
{
    public List<ReturnHeaderDto> Returns { get; init; } = new();
    public List<ReturnLineDto> Lines { get; init; } = new();
    public List<ReturnLocationDto> Locations { get; init; } = new();
    public List<ReasonCountDto> Reasons { get; init; } = new();
    public List<string> ReasonCodes { get; init; } = new();
    public string? ScrapNumber { get; init; }
}

public class GetReturnsRequest : IRequest<GetReturnsResult>
{
    public string? SalesReturnId { get; init; }
}

public class GetReturnsHandler : IRequestHandler<GetReturnsRequest, GetReturnsResult>
{
    private readonly IQueryContext _context;

    public GetReturnsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetReturnsResult> Handle(GetReturnsRequest request, CancellationToken ct)
    {
        var heads = await _context.SalesReturn.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(r => r.Status == SalesReturnStatus.Confirmed)
            .OrderByDescending(r => r.ReturnDate).Take(200)
            .Select(r => new { r.Id, r.Number, r.ReturnDate, Customer = r.DeliveryOrder!.SalesOrder!.Customer!.Name })
            .ToListAsync(ct);
        var ids = heads.Select(h => h.Id).ToList();

        var tx = await _context.InventoryTransaction.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(x => x.ModuleName == nameof(Domain.Entities.SalesReturn) && ids.Contains(x.ModuleId))
            .Select(x => new { x.ModuleId, x.ProductId, x.WarehouseId, Qty = x.Movement ?? 0 }).ToListAsync(ct);
        var insp = await _context.ReturnInspection.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(i => ids.Contains(i.SalesReturnId)).ToListAsync(ct);

        var since = DateTime.UtcNow.AddDays(-90);
        var reasons = insp.Where(i => i.CreatedAtUtc >= since && !string.IsNullOrEmpty(i.ReasonCode))
            .GroupBy(i => i.ReasonCode!).Select(g => new ReasonCountDto { Reason = g.Key, Units = g.Sum(i => i.ReturnedQuantity ?? 0) })
            .OrderByDescending(r => r.Units).ToList();

        var result = new GetReturnsResult
        {
            ReasonCodes = ReturnReasons.All.ToList(),
            Reasons = reasons,
            Returns = heads.Select(h => new ReturnHeaderDto
            {
                Id = h.Id, Number = h.Number, ReturnDate = h.ReturnDate, Customer = h.Customer,
                Units = tx.Where(x => x.ModuleId == h.Id).Sum(x => Math.Abs(x.Qty)),
                Inspected = insp.Any(i => i.SalesReturnId == h.Id),
                Damaged = insp.Where(i => i.SalesReturnId == h.Id).Sum(i => i.DamagedQuantity ?? 0)
            }).ToList()
        };

        var id = request.SalesReturnId;
        if (string.IsNullOrEmpty(id) || !ids.Contains(id)) return result;

        var mine = tx.Where(x => x.ModuleId == id).ToList();
        var warehouseId = mine.Select(x => x.WarehouseId).FirstOrDefault(w => w != null);
        var productIds = mine.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Product.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(ct);
        var locations = await _context.StorageLocation.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(l => l.IsActive && (warehouseId == null || l.WarehouseId == warehouseId))
            .OrderBy(l => l.Zone).ThenBy(l => l.Code)
            .Select(l => new ReturnLocationDto { Id = l.Id, Code = l.Code, Zone = l.Zone }).ToListAsync(ct);

        var number = heads.First(h => h.Id == id).Number;
        var scrapTag = $"Returned damaged - {number}";
        var scrapNumber = await _context.Scrapping.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(s => s.Description == scrapTag).Select(s => s.Number).FirstOrDefaultAsync(ct);

        var lines = mine.GroupBy(x => x.ProductId).Select(g =>
        {
            var p = products.FirstOrDefault(x => x.Id == g.Key);
            var i = insp.FirstOrDefault(x => x.SalesReturnId == id && x.ProductId == g.Key);
            return new ReturnLineDto
            {
                ProductId = g.Key, ProductNumber = p?.Number, ProductName = p?.Name, Returned = g.Sum(x => Math.Abs(x.Qty)),
                Inspected = i != null, Good = i?.GoodQuantity ?? 0, Damaged = i?.DamagedQuantity ?? 0, ReasonCode = i?.ReasonCode, Note = i?.Note, LocationId = i?.LocationId
            };
        }).ToList();

        return new GetReturnsResult
        {
            Returns = result.Returns, Reasons = reasons, ReasonCodes = result.ReasonCodes, Lines = lines, Locations = locations, ScrapNumber = scrapNumber
        };
    }
}
