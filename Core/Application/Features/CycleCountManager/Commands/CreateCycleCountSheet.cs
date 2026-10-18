using Application.Common.Repositories;
using Application.Features.InventoryTransactionManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.CycleCountManager.Commands;

public class CreateCycleCountSheetResult
{
    public string? StockCountId { get; set; }
    public string? Number { get; set; }
    public int Lines { get; set; }
}

public class CreateCycleCountSheetRequest : IRequest<CreateCycleCountSheetResult>
{
    public string? WarehouseId { get; init; }
    public List<string>? ProductIds { get; init; }
    public string? Description { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateCycleCountSheetValidator : AbstractValidator<CreateCycleCountSheetRequest>
{
    public CreateCycleCountSheetValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductIds).NotEmpty().WithMessage("Choose at least one product to count.");
    }
}

/// <summary>
/// Opens a Draft stock count for the chosen products, each line starting at the system quantity.
/// The counter then types the real numbers on the Stock Count page and confirms it; the existing
/// stock count process books the differences.
/// </summary>
public class CreateCycleCountSheetHandler : IRequestHandler<CreateCycleCountSheetRequest, CreateCycleCountSheetResult>
{
    private readonly ICommandRepository<StockCount> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly InventoryTransactionService _inventoryTransactionService;

    public CreateCycleCountSheetHandler(ICommandRepository<StockCount> repository, IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService, InventoryTransactionService inventoryTransactionService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
        _inventoryTransactionService = inventoryTransactionService;
    }

    public async Task<CreateCycleCountSheetResult> Handle(CreateCycleCountSheetRequest request, CancellationToken ct)
    {
        var ids = (request.ProductIds ?? new List<string>()).Distinct().ToList();
        if (ids.Count == 0) throw new Exception("Choose at least one product to count.");
        if (ids.Count > 300) throw new Exception("A count sheet can hold at most 300 products. Split it into two.");

        var entity = new StockCount
        {
            CreatedById = request.CreatedById,
            Number = _numberSequenceService.GenerateNumber(nameof(StockCount), "", "SC"),
            CountDate = DateTime.Today,
            Status = StockCountStatus.Draft,
            Description = string.IsNullOrWhiteSpace(request.Description) ? "Cycle count" : request.Description,
            WarehouseId = request.WarehouseId
        };
        await _repository.CreateAsync(entity, ct);
        await _unitOfWork.SaveAsync(ct);

        var lines = 0;
        foreach (var pid in ids)
        {
            var systemQty = _inventoryTransactionService.GetStock(request.WarehouseId, pid);
            if (systemQty <= 0) continue; // a line with a zero count is not allowed by the count process
            await _inventoryTransactionService.StockCountCreateInvenTrans(entity.Id, pid, systemQty, request.CreatedById, ct);
            lines++;
        }

        if (lines == 0) throw new Exception("None of the chosen products has stock in this warehouse to count.");
        return new CreateCycleCountSheetResult { StockCountId = entity.Id, Number = entity.Number, Lines = lines };
    }
}
