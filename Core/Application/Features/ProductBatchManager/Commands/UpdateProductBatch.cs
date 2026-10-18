using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.ProductBatchManager.Commands;

public class UpdateProductBatchResult
{
    public ProductBatch? Data { get; set; }
}

public class UpdateProductBatchRequest : IRequest<UpdateProductBatchResult>
{
    public string? Id { get; init; }
    public string? ProductId { get; init; }
    public string? WarehouseId { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ManufactureDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public double? Quantity { get; init; }
    public string? Notes { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdateProductBatchValidator : AbstractValidator<UpdateProductBatchRequest>
{
    public UpdateProductBatchValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Quantity).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpiryDate)
            .GreaterThanOrEqualTo(x => x.ManufactureDate!.Value)
            .When(x => x.ExpiryDate.HasValue && x.ManufactureDate.HasValue)
            .WithMessage("Expiry date must be on or after the manufacture date.");
    }
}

public class UpdateProductBatchHandler : IRequestHandler<UpdateProductBatchRequest, UpdateProductBatchResult>
{
    private readonly ICommandRepository<ProductBatch> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductBatchHandler(ICommandRepository<ProductBatch> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateProductBatchResult> Handle(UpdateProductBatchRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);
        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        var batchNumber = request.BatchNumber!.Trim();
        var duplicate = _repository.GetQuery()
            .ApplyIsDeletedFilter()
            .Any(x => x.Id != entity.Id && x.ProductId == request.ProductId && x.BatchNumber == batchNumber);
        if (duplicate)
        {
            throw new Exception($"Batch {batchNumber} already exists for this product.");
        }

        entity.UpdatedById = request.UpdatedById;
        entity.ProductId = request.ProductId;
        entity.WarehouseId = string.IsNullOrWhiteSpace(request.WarehouseId) ? null : request.WarehouseId;
        entity.BatchNumber = batchNumber;
        entity.ManufactureDate = request.ManufactureDate;
        entity.ExpiryDate = request.ExpiryDate;
        entity.Quantity = request.Quantity;
        entity.Notes = request.Notes;

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new UpdateProductBatchResult { Data = entity };
    }
}
