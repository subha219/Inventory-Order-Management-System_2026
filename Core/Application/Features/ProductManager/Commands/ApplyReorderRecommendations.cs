using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.ProductManager.Commands;

public class ReorderRecommendation
{
    public string? ProductId { get; init; }
    public double ReorderPoint { get; init; }
    public double ReorderQuantity { get; init; }
}

public class ApplyReorderRecommendationsResult
{
    public int Updated { get; init; }
}

public class ApplyReorderRecommendationsRequest : IRequest<ApplyReorderRecommendationsResult>
{
    public List<ReorderRecommendation>? Items { get; init; }
    public string? UpdatedById { get; init; }
}

public class ApplyReorderRecommendationsValidator : AbstractValidator<ApplyReorderRecommendationsRequest>
{
    public ApplyReorderRecommendationsValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(r => r.ProductId).NotEmpty();
            i.RuleFor(r => r.ReorderPoint).GreaterThanOrEqualTo(0);
            i.RuleFor(r => r.ReorderQuantity).GreaterThanOrEqualTo(0);
        });
    }
}

public class ApplyReorderRecommendationsHandler : IRequestHandler<ApplyReorderRecommendationsRequest, ApplyReorderRecommendationsResult>
{
    private readonly ICommandRepository<Product> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyReorderRecommendationsHandler(ICommandRepository<Product> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplyReorderRecommendationsResult> Handle(ApplyReorderRecommendationsRequest request, CancellationToken cancellationToken)
    {
        var updated = 0;
        foreach (var item in request.Items!)
        {
            var entity = await _repository.GetAsync(item.ProductId ?? string.Empty, cancellationToken);
            if (entity == null) continue;

            entity.UpdatedById = request.UpdatedById;
            entity.ReorderPoint = item.ReorderPoint;
            entity.ReorderQuantity = item.ReorderQuantity;
            _repository.Update(entity);
            updated++;
        }

        await _unitOfWork.SaveAsync(cancellationToken);
        return new ApplyReorderRecommendationsResult { Updated = updated };
    }
}
