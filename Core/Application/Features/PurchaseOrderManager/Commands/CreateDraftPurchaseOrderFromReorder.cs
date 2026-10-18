using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.PurchaseOrderManager.Commands;

public class ReorderLine
{
    public string? ProductId { get; init; }
    public double Quantity { get; init; }
}

public class CreateDraftPurchaseOrderFromReorderResult
{
    public string? PurchaseOrderId { get; init; }
    public string? Number { get; init; }
    public int LineCount { get; init; }
    public double? AfterTaxAmount { get; init; }
}

public class CreateDraftPurchaseOrderFromReorderRequest : IRequest<CreateDraftPurchaseOrderFromReorderResult>
{
    public string? VendorId { get; init; }
    public string? TaxId { get; init; }
    public List<ReorderLine>? Lines { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateDraftPurchaseOrderFromReorderValidator : AbstractValidator<CreateDraftPurchaseOrderFromReorderRequest>
{
    public CreateDraftPurchaseOrderFromReorderValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.TaxId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty();
            l.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

public class CreateDraftPurchaseOrderFromReorderHandler
    : IRequestHandler<CreateDraftPurchaseOrderFromReorderRequest, CreateDraftPurchaseOrderFromReorderResult>
{
    private readonly ICommandRepository<PurchaseOrder> _orderRepository;
    private readonly ICommandRepository<PurchaseOrderItem> _itemRepository;
    private readonly ICommandRepository<Product> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly PurchaseOrderService _purchaseOrderService;

    public CreateDraftPurchaseOrderFromReorderHandler(
        ICommandRepository<PurchaseOrder> orderRepository,
        ICommandRepository<PurchaseOrderItem> itemRepository,
        ICommandRepository<Product> productRepository,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService,
        PurchaseOrderService purchaseOrderService)
    {
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
        _purchaseOrderService = purchaseOrderService;
    }

    public async Task<CreateDraftPurchaseOrderFromReorderResult> Handle(
        CreateDraftPurchaseOrderFromReorderRequest request, CancellationToken cancellationToken)
    {
        var lines = request.Lines!
            .GroupBy(l => l.ProductId!)
            .Select(g => new ReorderLine { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        var productIds = lines.Select(l => l.ProductId!).ToList();
        var products = _productRepository.GetQuery()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id);

        var order = new PurchaseOrder
        {
            CreatedById = request.CreatedById,
            Number = _numberSequenceService.GenerateNumber(nameof(PurchaseOrder), "", "PO"),
            OrderDate = DateTime.UtcNow,
            OrderStatus = PurchaseOrderStatus.Draft,
            Description = "Created from reorder suggestions",
            VendorId = request.VendorId,
            TaxId = request.TaxId
        };

        await _orderRepository.CreateAsync(order, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        foreach (var line in lines)
        {
            products.TryGetValue(line.ProductId!, out var product);
            var unitPrice = product?.UnitPrice ?? 0;

            await _itemRepository.CreateAsync(new PurchaseOrderItem
            {
                CreatedById = request.CreatedById,
                PurchaseOrderId = order.Id,
                ProductId = line.ProductId,
                Summary = product?.Name,
                UnitPrice = unitPrice,
                Quantity = line.Quantity,
                Total = line.Quantity * unitPrice
            }, cancellationToken);
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        _purchaseOrderService.Recalculate(order.Id);

        return new CreateDraftPurchaseOrderFromReorderResult
        {
            PurchaseOrderId = order.Id,
            Number = order.Number,
            LineCount = lines.Count,
            AfterTaxAmount = order.AfterTaxAmount
        };
    }
}
