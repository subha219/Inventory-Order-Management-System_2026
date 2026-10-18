using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.ProductBatchManager.Commands;

public class CreateProductBatchResult
{
    public ProductBatch? Data { get; set; }
}

public class CreateProductBatchRequest : IRequest<CreateProductBatchResult>
{
    public string? ProductId { get; init; }
    public string? WarehouseId { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ManufactureDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public double? Quantity { get; init; }
    public string? Notes { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateProductBatchValidator : AbstractValidator<CreateProductBatchRequest>
{
    public CreateProductBatchValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Quantity).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpiryDate)
            .GreaterThanOrEqualTo(x => x.ManufactureDate!.Value)
            .When(x => x.ExpiryDate.HasValue && x.ManufactureDate.HasValue)
            .WithMessage("Expiry date must be on or after the manufacture date.");
    }
}

public class CreateProductBatchHandler : IRequestHandler<CreateProductBatchRequest, CreateProductBatchResult>
{
    private readonly ICommandRepository<ProductBatch> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductBatchHandler(ICommandRepository<ProductBatch> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateProductBatchResult> Handle(CreateProductBatchRequest request, CancellationToken cancellationToken)
    {
        var batchNumber = request.BatchNumber!.Trim();

        var exists = _repository.GetQuery()
            .ApplyIsDeletedFilter()
            .Any(x => x.ProductId == request.ProductId && x.BatchNumber == batchNumber);
        if (exists)
        {
            throw new Exception($"Batch {batchNumber} already exists for this product.");
        }

        var entity = new ProductBatch
        {
            CreatedById = request.CreatedById,
            ProductId = request.ProductId,
            WarehouseId = string.IsNullOrWhiteSpace(request.WarehouseId) ? null : request.WarehouseId,
            BatchNumber = batchNumber,
            ManufactureDate = request.ManufactureDate,
            ExpiryDate = request.ExpiryDate,
            Quantity = request.Quantity,
            Notes = request.Notes
        };

        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new CreateProductBatchResult { Data = entity };
    }
}
