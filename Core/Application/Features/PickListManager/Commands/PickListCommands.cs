using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.PickListManager.Commands;

public class PickLineInput
{
    public string? ProductId { get; init; }
    public string? LocationId { get; init; }
    public double Quantity { get; init; }
}

public class BatchPickInput
{
    public string? ProductId { get; init; }
    public string? BatchId { get; init; }
    public double Quantity { get; init; }
}

public class ConfirmPickResult
{
    public int Lines { get; set; }
    public double Units { get; set; }
}

public class ConfirmPickRequest : IRequest<ConfirmPickResult>
{
    public string? DeliveryOrderId { get; init; }
    public List<PickLineInput>? Picks { get; init; }
    /// <summary>Which batches the goods came from (first expiry first); optional.</summary>
    public List<BatchPickInput>? BatchPicks { get; init; }
    public string? UpdatedById { get; init; }
}

public class ConfirmPickValidator : AbstractValidator<ConfirmPickRequest>
{
    public ConfirmPickValidator()
    {
        RuleFor(x => x.DeliveryOrderId).NotEmpty();
        RuleFor(x => x.Picks).NotEmpty().WithMessage("Choose at least one location to pick from.");
    }
}

/// <summary>
/// Records which locations the goods were taken from and reduces the stock held in those locations.
/// The warehouse total is untouched here: it already falls when the delivery order is confirmed.
/// </summary>
public class ConfirmPickHandler : IRequestHandler<ConfirmPickRequest, ConfirmPickResult>
{
    private readonly ICommandRepository<DeliveryOrder> _deliveries;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly ICommandRepository<LocationStock> _stocks;
    private readonly ICommandRepository<DeliveryPick> _picks;
    private readonly ICommandRepository<StorageLocation> _locations;
    private readonly ICommandRepository<ProductBatch> _batches;
    private readonly ICommandRepository<DeliveryPickBatch> _batchPicks;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmPickHandler(
        ICommandRepository<DeliveryOrder> deliveries,
        ICommandRepository<InventoryTransaction> transactions,
        ICommandRepository<LocationStock> stocks,
        ICommandRepository<DeliveryPick> picks,
        ICommandRepository<StorageLocation> locations,
        ICommandRepository<ProductBatch> batches,
        ICommandRepository<DeliveryPickBatch> batchPicks,
        IUnitOfWork unitOfWork)
    {
        _deliveries = deliveries;
        _transactions = transactions;
        _stocks = stocks;
        _picks = picks;
        _locations = locations;
        _batches = batches;
        _batchPicks = batchPicks;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConfirmPickResult> Handle(ConfirmPickRequest request, CancellationToken ct)
    {
        var delivery = await _deliveries.GetAsync(request.DeliveryOrderId ?? string.Empty, ct);
        if (delivery == null) throw new Exception("Delivery order not found.");
        if (delivery.Status == DeliveryOrderStatus.Cancelled || delivery.Status == DeliveryOrderStatus.Archived)
            throw new Exception($"{delivery.Number} is {delivery.Status}; nothing can be picked for it.");

        var needed = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ModuleId == delivery.Id && x.ModuleName == nameof(DeliveryOrder))
            .Select(x => new { x.ProductId, Qty = x.Movement ?? 0 })
            .ToList()
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key ?? string.Empty, g => g.Sum(x => Math.Abs(x.Qty)));

        var already = _picks.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.DeliveryOrderId == delivery.Id)
            .Select(x => new { x.ProductId, Qty = x.Quantity ?? 0 })
            .ToList()
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key ?? string.Empty, g => g.Sum(x => x.Qty));

        var lines = (request.Picks ?? new List<PickLineInput>()).Where(p => p.Quantity > 0).ToList();
        var batchLines = (request.BatchPicks ?? new List<BatchPickInput>()).Where(p => p.Quantity > 0).ToList();
        if (lines.Count == 0) throw new Exception("Enter a quantity to pick.");

        // batch quantities must be covered by the units picked in this step
        foreach (var g in batchLines.GroupBy(b => b.ProductId ?? string.Empty))
        {
            var pickedNow = lines.Where(l => (l.ProductId ?? string.Empty) == g.Key).Sum(l => l.Quantity);
            if (g.Sum(b => b.Quantity) > pickedNow + 1e-9)
                throw new Exception($"You took {g.Sum(b => b.Quantity):0.##} from batches but are only picking {pickedNow:0.##} units of that product.");
        }

        // never pick more of a product than the delivery order asks for
        foreach (var g in lines.GroupBy(l => l.ProductId ?? string.Empty))
        {
            if (!needed.TryGetValue(g.Key, out var need)) throw new Exception("A picked product is not on this delivery order.");
            already.TryGetValue(g.Key, out var done);
            var asked = g.Sum(l => l.Quantity);
            if (asked > need - done + 1e-9)
                throw new Exception($"You are picking {asked:0.##} of a product but only {Math.Max(need - done, 0):0.##} is still to pick.");
        }

        var result = new ConfirmPickResult();
        foreach (var l in lines)
        {
            var location = await _locations.GetAsync(l.LocationId ?? string.Empty, ct) ?? throw new Exception("Location not found.");
            var row = _stocks.GetQuery().ApplyIsDeletedFilter()
                .FirstOrDefault(x => x.LocationId == location.Id && x.ProductId == l.ProductId);
            var have = row?.Quantity ?? 0;
            if (row == null || l.Quantity > have + 1e-9)
                throw new Exception($"Location {location.Code} only holds {have:0.##} of that product.");

            var left = have - l.Quantity;
            row.UpdatedById = request.UpdatedById;
            if (left <= 1e-9) _stocks.Delete(row);
            else { row.Quantity = left; _stocks.Update(row); }

            await _picks.CreateAsync(new DeliveryPick
            {
                CreatedById = request.UpdatedById, DeliveryOrderId = delivery.Id, ProductId = l.ProductId,
                LocationId = location.Id, Quantity = l.Quantity
            }, ct);
            result.Lines++;
            result.Units += l.Quantity;
        }

        var today = DateTime.UtcNow.Date;
        foreach (var b in batchLines)
        {
            var batch = await _batches.GetAsync(b.BatchId ?? string.Empty, ct) ?? throw new Exception("Batch not found.");
            if (batch.ProductId != b.ProductId) throw new Exception($"Batch {batch.BatchNumber} belongs to a different product.");
            if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < today) throw new Exception($"Batch {batch.BatchNumber} has expired and cannot be shipped.");
            var have = batch.Quantity ?? 0;
            if (b.Quantity > have + 1e-9) throw new Exception($"Batch {batch.BatchNumber} only holds {have:0.##}.");
            batch.UpdatedById = request.UpdatedById;
            batch.Quantity = have - b.Quantity;
            _batches.Update(batch);
            await _batchPicks.CreateAsync(new DeliveryPickBatch
            {
                CreatedById = request.UpdatedById, DeliveryOrderId = delivery.Id, ProductId = b.ProductId, BatchId = batch.Id, Quantity = b.Quantity
            }, ct);
        }

        await _unitOfWork.SaveAsync(ct);
        return result;
    }
}
