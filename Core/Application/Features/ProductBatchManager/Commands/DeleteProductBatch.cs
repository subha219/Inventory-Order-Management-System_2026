using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.ProductBatchManager.Commands;

public class DeleteProductBatchResult
{
    public ProductBatch? Data { get; set; }
}

public class DeleteProductBatchRequest : IRequest<DeleteProductBatchResult>
{
    public string? Id { get; init; }
    public string? DeletedById { get; init; }
}

public class DeleteProductBatchValidator : AbstractValidator<DeleteProductBatchRequest>
{
    public DeleteProductBatchValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteProductBatchHandler : IRequestHandler<DeleteProductBatchRequest, DeleteProductBatchResult>
{
    private readonly ICommandRepository<ProductBatch> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProductBatchHandler(ICommandRepository<ProductBatch> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteProductBatchResult> Handle(DeleteProductBatchRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);
        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.DeletedById;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new DeleteProductBatchResult { Data = entity };
    }
}
