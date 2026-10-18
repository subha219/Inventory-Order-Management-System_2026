using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.PortalManager.Queries;

public class PortalResponseDto
{
    public string? Status { get; init; }
    public DateTime? ExpectedDate { get; init; }
    public string? Message { get; init; }
    public DateTime? At { get; init; }
}

public class PortalItemDto
{
    public string? ProductName { get; init; }
    public double Quantity { get; init; }
    public double UnitPrice { get; init; }
    public double Total { get; init; }
}

public class PortalShipmentDto
{
    public string? Number { get; init; }
    public string? Status { get; init; }
    public string? Carrier { get; init; }
    public string? TrackingNumber { get; init; }
    public DateTime? ShippedAtUtc { get; init; }
    public string? TrackToken { get; init; }
}

public class PortalViewResult
{
    public bool Found { get; init; }
    public bool Expired { get; init; }
    /// <summary>Vendor or Customer.</summary>
    public string? Kind { get; init; }
    public string? Number { get; init; }
    public string? PartyName { get; init; }
    public string? CompanyName { get; init; }
    public DateTime? OrderDate { get; init; }
    public string? Status { get; init; }
    public double Total { get; init; }
    public List<PortalItemDto> Items { get; init; } = new();
    public List<PortalShipmentDto> Shipments { get; init; } = new();
    public List<PortalResponseDto> Responses { get; init; } = new();
}

public class GetPortalViewRequest : IRequest<PortalViewResult>
{
    public string? Token { get; init; }
}

public class GetPortalViewHandler : IRequestHandler<GetPortalViewRequest, PortalViewResult>
{
    private readonly IQueryContext _context;

    public GetPortalViewHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<PortalViewResult> Handle(GetPortalViewRequest r, CancellationToken ct)
    {
        var token = r.Token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length < 16 || token.Length > 64) return new PortalViewResult { Found = false };
        var link = await _context.PortalLink.AsNoTracking().ApplyIsDeletedFilter(false).FirstOrDefaultAsync(l => l.Token == token, ct);
        if (link == null || link.IsActive != true) return new PortalViewResult { Found = false };
        if (link.ExpiresAtUtc != null && link.ExpiresAtUtc < DateTime.UtcNow) return new PortalViewResult { Found = true, Expired = true };

        var company = await _context.Company.AsNoTracking().ApplyIsDeletedFilter(false).Select(c => c.Name).FirstOrDefaultAsync(ct);
        var responses = await _context.PortalResponse.AsNoTracking().ApplyIsDeletedFilter(false).Where(x => x.PortalLinkId == link.Id).OrderByDescending(x => x.CreatedAtUtc).Take(10).ToListAsync(ct);
        var rdto = responses.Select(x => new PortalResponseDto { Status = x.Status, ExpectedDate = x.ExpectedDate, Message = x.Message, At = x.CreatedAtUtc }).ToList();

        if (link.EntityType == "PurchaseOrder")
        {
            var po = await _context.PurchaseOrder.AsNoTracking().Include(p => p.Vendor).FirstOrDefaultAsync(p => p.Id == link.EntityId, ct);
            if (po == null) return new PortalViewResult { Found = false };
            var items = await _context.PurchaseOrderItem.AsNoTracking().ApplyIsDeletedFilter(false).Include(i => i.Product).Where(i => i.PurchaseOrderId == po.Id).ToListAsync(ct);
            return new PortalViewResult
            {
                Found = true, Kind = "Vendor", Number = po.Number, PartyName = po.Vendor?.Name, CompanyName = company, OrderDate = po.OrderDate, Status = po.OrderStatus?.ToString(),
                Total = po.AfterTaxAmount ?? 0, Responses = rdto,
                Items = items.Select(i => new PortalItemDto { ProductName = i.Product?.Name ?? i.Summary, Quantity = i.Quantity ?? 0, UnitPrice = i.UnitPrice ?? 0, Total = i.Total ?? 0 }).ToList()
            };
        }
        else
        {
            var so = await _context.SalesOrder.AsNoTracking().Include(p => p.Customer).FirstOrDefaultAsync(p => p.Id == link.EntityId, ct);
            if (so == null) return new PortalViewResult { Found = false };
            var items = await _context.SalesOrderItem.AsNoTracking().ApplyIsDeletedFilter(false).Include(i => i.Product).Where(i => i.SalesOrderId == so.Id).ToListAsync(ct);
            var doIds = await _context.DeliveryOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(d => d.SalesOrderId == so.Id).Select(d => d.Id).ToListAsync(ct);
            var ships = await _context.Shipment.AsNoTracking().ApplyIsDeletedFilter(false).Where(s => doIds.Contains(s.DeliveryOrderId!)).OrderBy(s => s.CreatedAtUtc).ToListAsync(ct);
            return new PortalViewResult
            {
                Found = true, Kind = "Customer", Number = so.Number, PartyName = so.Customer?.Name, CompanyName = company, OrderDate = so.OrderDate, Status = so.OrderStatus?.ToString(),
                Total = so.AfterTaxAmount ?? 0, Responses = rdto,
                Items = items.Select(i => new PortalItemDto { ProductName = i.Product?.Name ?? i.Summary, Quantity = i.Quantity ?? 0, UnitPrice = i.UnitPrice ?? 0, Total = i.Total ?? 0 }).ToList(),
                Shipments = ships.Select(s => new PortalShipmentDto
                {
                    Number = s.Number, Status = s.Status, Carrier = s.Carrier, TrackingNumber = s.TrackingNumber,
                    ShippedAtUtc = s.ShippedAtUtc == null ? null : DateTime.SpecifyKind(s.ShippedAtUtc.Value, DateTimeKind.Utc), TrackToken = s.PublicToken
                }).ToList()
            };
        }
    }
}

// ---------------------------------------------------------------- staff side
public class PortalLinkDto
{
    public string? Id { get; init; }
    public string? EntityType { get; init; }
    public string? OrderNumber { get; init; }
    public string? PartyName { get; init; }
    public string? Token { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public bool Active { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public List<PortalResponseDto> Responses { get; init; } = new();
}

public class PortalOrderOptionDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? PartyName { get; init; }
    public string? Status { get; init; }
}

public class GetPortalAdminResult
{
    public List<PortalLinkDto> Links { get; init; } = new();
    public List<PortalOrderOptionDto> PurchaseOrders { get; init; } = new();
    public List<PortalOrderOptionDto> SalesOrders { get; init; } = new();
}

public class GetPortalAdminRequest : IRequest<GetPortalAdminResult>
{
}

public class GetPortalAdminHandler : IRequestHandler<GetPortalAdminRequest, GetPortalAdminResult>
{
    private readonly IQueryContext _context;

    public GetPortalAdminHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetPortalAdminResult> Handle(GetPortalAdminRequest request, CancellationToken ct)
    {
        var pos = await _context.PurchaseOrder.AsNoTracking().ApplyIsDeletedFilter(false).Include(p => p.Vendor).OrderByDescending(p => p.CreatedAtUtc).Take(60).ToListAsync(ct);
        var sos = await _context.SalesOrder.AsNoTracking().ApplyIsDeletedFilter(false).Include(p => p.Customer).OrderByDescending(p => p.CreatedAtUtc).Take(60).ToListAsync(ct);
        var links = await _context.PortalLink.AsNoTracking().ApplyIsDeletedFilter(false).OrderByDescending(l => l.CreatedAtUtc).Take(60).ToListAsync(ct);
        var ids = links.Select(l => l.Id).ToList();
        var resp = await _context.PortalResponse.AsNoTracking().ApplyIsDeletedFilter(false).Where(x => ids.Contains(x.PortalLinkId!)).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var linkPoIds = links.Where(l => l.EntityType == "PurchaseOrder").Select(l => l.EntityId).ToList();
        var linkSoIds = links.Where(l => l.EntityType == "SalesOrder").Select(l => l.EntityId).ToList();
        var linkPos = await _context.PurchaseOrder.AsNoTracking().Include(p => p.Vendor).Where(p => linkPoIds.Contains(p.Id)).ToListAsync(ct);
        var linkSos = await _context.SalesOrder.AsNoTracking().Include(p => p.Customer).Where(p => linkSoIds.Contains(p.Id)).ToListAsync(ct);
        var poById = linkPos.ToDictionary(p => p.Id); var soById = linkSos.ToDictionary(s => s.Id);

        return new GetPortalAdminResult
        {
            PurchaseOrders = pos.Select(p => new PortalOrderOptionDto { Id = p.Id, Number = p.Number, PartyName = p.Vendor?.Name, Status = p.OrderStatus?.ToString() }).ToList(),
            SalesOrders = sos.Select(s => new PortalOrderOptionDto { Id = s.Id, Number = s.Number, PartyName = s.Customer?.Name, Status = s.OrderStatus?.ToString() }).ToList(),
            Links = links.Select(l =>
            {
                string? no = null, party = null;
                if (l.EntityType == "PurchaseOrder" && poById.TryGetValue(l.EntityId ?? "", out var p)) { no = p.Number; party = p.Vendor?.Name; }
                if (l.EntityType == "SalesOrder" && soById.TryGetValue(l.EntityId ?? "", out var s)) { no = s.Number; party = s.Customer?.Name; }
                return new PortalLinkDto
                {
                    Id = l.Id, EntityType = l.EntityType, OrderNumber = no, PartyName = party, Token = l.Token, ExpiresAtUtc = l.ExpiresAtUtc,
                    Active = l.IsActive == true && (l.ExpiresAtUtc == null || l.ExpiresAtUtc > DateTime.UtcNow), CreatedAtUtc = l.CreatedAtUtc,
                    Responses = resp.Where(x => x.PortalLinkId == l.Id).Select(x => new PortalResponseDto { Status = x.Status, ExpectedDate = x.ExpectedDate, Message = x.Message, At = x.CreatedAtUtc }).ToList()
                };
            }).ToList()
        };
    }
}
