using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.PurchaseOrderManager.Queries;

public class ReorderSuggestionItem
{
    public string? ProductId { get; init; }
    public string? ProductNumber { get; init; }
    public string? ProductName { get; init; }
    public double Stock { get; init; }
    public double ReorderPoint { get; init; }
    /// <summary>Quantity already sitting on Draft purchase orders.</summary>
    public double OnDraftOrders { get; init; }
    public double SuggestedQuantity { get; init; }
    public double UnitPrice { get; init; }
}

public class GetReorderSuggestionsResult
{
    public List<ReorderSuggestionItem>? Data { get; init; }
}

public class GetReorderSuggestionsRequest : IRequest<GetReorderSuggestionsResult>
{
}

/// <summary>
/// Products at or below their reorder point. Suggested quantity is the product's ReorderQuantity when set,
/// otherwise enough to bring stock back up to twice the reorder point.
/// </summary>
public class GetReorderSuggestionsHandler : IRequestHandler<GetReorderSuggestionsRequest, GetReorderSuggestionsResult>
{
    private readonly IQueryContext _context;

    public GetReorderSuggestionsHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetReorderSuggestionsResult> Handle(GetReorderSuggestionsRequest request, CancellationToken cancellationToken)
    {
        var stockRows = await _context.InventoryTransaction
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Where(x =>
                x.Status == InventoryTransactionStatus.Confirmed &&
                x.Warehouse!.SystemWarehouse == false &&
                x.Product!.Physical == true)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(x => x.Stock) })
            .ToListAsync(cancellationToken);

        var stockByProduct = stockRows
            .Where(x => x.ProductId != null)
            .ToDictionary(x => x.ProductId!, x => x.Stock ?? 0.0);

        var draftRows = await _context.PurchaseOrderItem
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Where(x => x.PurchaseOrder!.OrderStatus == PurchaseOrderStatus.Draft && x.PurchaseOrder.IsDeleted == false)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);

        var draftByProduct = draftRows
            .Where(x => x.ProductId != null)
            .ToDictionary(x => x.ProductId!, x => x.Quantity ?? 0.0);

        var products = await _context.Product
            .AsNoTracking()
            .ApplyIsDeletedFilter(false)
            .Where(x => x.Physical == true && x.ReorderPoint != null)
            .Select(x => new { x.Id, x.Number, x.Name, x.UnitPrice, x.ReorderPoint, x.ReorderQuantity })
            .ToListAsync(cancellationToken);

        var list = products
            .Select(p =>
            {
                var stock = stockByProduct.TryGetValue(p.Id, out var s) ? s : 0.0;
                var reorderPoint = p.ReorderPoint!.Value;
                var suggested = p.ReorderQuantity is > 0
                    ? p.ReorderQuantity.Value
                    : Math.Max(Math.Ceiling(reorderPoint * 2 - stock), 1);
                return new ReorderSuggestionItem
                {
                    ProductId = p.Id,
                    ProductNumber = p.Number,
                    ProductName = p.Name,
                    Stock = stock,
                    ReorderPoint = reorderPoint,
                    OnDraftOrders = draftByProduct.TryGetValue(p.Id, out var d) ? d : 0.0,
                    SuggestedQuantity = suggested,
                    UnitPrice = p.UnitPrice ?? 0
                };
            })
            .Where(x => x.Stock <= x.ReorderPoint)
            .OrderBy(x => x.Stock - x.ReorderPoint)
            .ToList();

        return new GetReorderSuggestionsResult { Data = list };
    }
}
