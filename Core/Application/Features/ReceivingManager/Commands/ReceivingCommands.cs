using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.InventoryTransactionManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.ReceivingManager.Commands;

public class ReceivingResult
{
    public int Lines { get; set; }
    public double Units { get; set; }
    /// <summary>Number of the draft scrapping document made for units that failed inspection, if any.</summary>
    public string? ScrapNumber { get; set; }
    public double ScrapUnits { get; set; }
    public string? ScrapNote { get; set; }
}

internal static class ReceivingHelper
{
    /// <summary>Units of each product on a confirmed goods receipt.</summary>
    public static Dictionary<string, double> Received(ICommandRepository<InventoryTransaction> transactions, string receiveId)
    {
        return transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ModuleId == receiveId && x.ModuleName == nameof(GoodsReceive))
            .Select(x => new { x.ProductId, Qty = x.Movement ?? 0 })
            .ToList()
            .GroupBy(x => x.ProductId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Abs(x.Qty)));
    }
}

// ---------------------------------------------------------------- quality inspection
public class InspectionInput
{
    public string? ProductId { get; init; }
    public double Passed { get; init; }
    public double Failed { get; init; }
    public string? Reason { get; init; }
}

public class SaveInspectionRequest : IRequest<ReceivingResult>
{
    public string? GoodsReceiveId { get; init; }
    public List<InspectionInput>? Lines { get; init; }
    public string? UpdatedById { get; init; }
}

public class SaveInspectionValidator : AbstractValidator<SaveInspectionRequest>
{
    public SaveInspectionValidator()
    {
        RuleFor(x => x.GoodsReceiveId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Enter the inspection result for at least one product.");
    }
}

public class SaveInspectionHandler : IRequestHandler<SaveInspectionRequest, ReceivingResult>
{
    private readonly ICommandRepository<GoodsReceive> _receipts;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly ICommandRepository<QualityInspection> _inspections;
    private readonly ICommandRepository<PutAwayRecord> _putAways;
    private readonly ICommandRepository<Scrapping> _scrappings;
    private readonly NumberSequenceService _numbers;
    private readonly InventoryTransactionService _inventory;
    private readonly IUnitOfWork _unitOfWork;

    public SaveInspectionHandler(ICommandRepository<GoodsReceive> receipts, ICommandRepository<InventoryTransaction> transactions,
        ICommandRepository<QualityInspection> inspections, ICommandRepository<PutAwayRecord> putAways, IUnitOfWork unitOfWork,
        ICommandRepository<Scrapping> scrappings, NumberSequenceService numbers, InventoryTransactionService inventory)
    {
        _scrappings = scrappings;
        _numbers = numbers;
        _inventory = inventory;
        _receipts = receipts;
        _transactions = transactions;
        _inspections = inspections;
        _putAways = putAways;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReceivingResult> Handle(SaveInspectionRequest request, CancellationToken ct)
    {
        var receipt = await _receipts.GetAsync(request.GoodsReceiveId ?? string.Empty, ct) ?? throw new Exception("Goods receipt not found.");
        var received = ReceivingHelper.Received(_transactions, receipt.Id);
        var existing = _inspections.GetQuery().ApplyIsDeletedFilter().Where(x => x.GoodsReceiveId == receipt.Id).ToList();
        var putAway = _putAways.GetQuery().ApplyIsDeletedFilter().Where(x => x.GoodsReceiveId == receipt.Id)
            .Select(x => new { x.ProductId, Qty = x.Quantity ?? 0 }).ToList();

        var result = new ReceivingResult();
        foreach (var l in request.Lines ?? new List<InspectionInput>())
        {
            var pid = l.ProductId ?? string.Empty;
            if (!received.TryGetValue(pid, out var got)) throw new Exception("A product is not on this goods receipt.");
            if (l.Passed < 0 || l.Failed < 0) throw new Exception("Quantities cannot be negative.");
            if (l.Passed + l.Failed > got + 1e-9) throw new Exception($"Passed plus failed ({l.Passed + l.Failed:0.##}) is more than the {got:0.##} received.");
            var placed = putAway.Where(x => x.ProductId == pid).Sum(x => x.Qty);
            if (l.Passed + 1e-9 < placed) throw new Exception($"{placed:0.##} units are already put away, so fewer than that cannot be marked as passed.");

            var result1 = l.Failed <= 0 ? "Pass" : l.Passed <= 0 ? "Fail" : "Partial";
            var row = existing.FirstOrDefault(x => x.ProductId == pid);
            if (row == null)
            {
                await _inspections.CreateAsync(new QualityInspection
                {
                    CreatedById = request.UpdatedById, GoodsReceiveId = receipt.Id, ProductId = pid,
                    PassedQuantity = l.Passed, FailedQuantity = l.Failed, Result = result1, Reason = l.Reason
                }, ct);
            }
            else
            {
                row.UpdatedById = request.UpdatedById;
                row.PassedQuantity = l.Passed; row.FailedQuantity = l.Failed; row.Result = result1; row.Reason = l.Reason;
                _inspections.Update(row);
            }
            result.Lines++;
            result.Units += l.Passed + l.Failed;
        }

        await _unitOfWork.SaveAsync(ct);
        await SyncScrapDraft(receipt, request.UpdatedById, result, ct);
        return result;
    }

    /// <summary>
    /// Units that failed inspection stay in the warehouse stock until they are written off. This keeps ONE draft scrapping
    /// document per receipt in step with the inspection (created, updated or emptied), so a person only has to confirm it.
    /// A document that is already confirmed or cancelled is never touched.
    /// </summary>
    private async Task SyncScrapDraft(GoodsReceive receipt, string? userId, ReceivingResult result, CancellationToken ct)
    {
        var tag = $"Failed inspection - {receipt.Number}";
        var failed = _inspections.GetQuery().ApplyIsDeletedFilter().Where(x => x.GoodsReceiveId == receipt.Id && (x.FailedQuantity ?? 0) > 0)
            .Select(x => new { x.ProductId, Qty = x.FailedQuantity ?? 0 }).ToList()
            .Where(x => x.ProductId != null).ToDictionary(x => x.ProductId!, x => x.Qty);

        var scrap = _scrappings.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(x => x.Description == tag);
        if (scrap == null && failed.Count == 0) return;

        if (scrap != null && scrap.Status != ScrappingStatus.Draft)
        {
            result.ScrapNumber = scrap.Number;
            result.ScrapNote = $"{scrap.Number} is already {scrap.Status}, so it was left as it is.";
            return;
        }

        var warehouseId = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ModuleId == receipt.Id && x.ModuleName == nameof(GoodsReceive))
            .Select(x => x.WarehouseId).ToList().FirstOrDefault(w => w != null);
        if (warehouseId == null) return;

        if (scrap == null)
        {
            scrap = new Scrapping
            {
                CreatedById = userId, Number = _numbers.GenerateNumber(nameof(Scrapping), "", "SCRP"),
                ScrappingDate = DateTime.Today, Status = ScrappingStatus.Draft, Description = tag, WarehouseId = warehouseId
            };
            await _scrappings.CreateAsync(scrap, ct);
            await _unitOfWork.SaveAsync(ct);
        }

        var lines = await _inventory.ScrappingGetInvenTransList(scrap.Id, nameof(Scrapping), ct);
        foreach (var line in lines)
        {
            var pid = line.ProductId ?? string.Empty;
            if (failed.TryGetValue(pid, out var qty))
            {
                if (Math.Abs((line.Movement ?? 0) - qty) > 1e-9) await _inventory.ScrappingUpdateInvenTrans(line.Id, pid, qty, userId, ct);
                failed.Remove(pid);
            }
            else await _inventory.ScrappingDeleteInvenTrans(line.Id, userId, ct);
        }
        foreach (var (pid, qty) in failed) await _inventory.ScrappingCreateInvenTrans(scrap.Id, pid, qty, userId, ct);

        var total = (await _inventory.ScrappingGetInvenTransList(scrap.Id, nameof(Scrapping), ct)).Sum(x => x.Movement ?? 0);
        result.ScrapNumber = scrap.Number;
        result.ScrapUnits = total;
        result.ScrapNote = total > 0
            ? $"Draft scrapping {scrap.Number} holds {total:0.##} failed unit(s). Review and confirm it on the Scrapping page to write them off."
            : $"{scrap.Number} has no failed units any more. You can delete or cancel it on the Scrapping page.";
    }
}

// ---------------------------------------------------------------- put-away
public class PutAwayInput
{
    public string? ProductId { get; init; }
    public string? LocationId { get; init; }
    public double Quantity { get; init; }
}

public class ConfirmPutAwayRequest : IRequest<ReceivingResult>
{
    public string? GoodsReceiveId { get; init; }
    public List<PutAwayInput>? Lines { get; init; }
    public string? UpdatedById { get; init; }
}

public class ConfirmPutAwayValidator : AbstractValidator<ConfirmPutAwayRequest>
{
    public ConfirmPutAwayValidator()
    {
        RuleFor(x => x.GoodsReceiveId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Choose at least one location to put stock into.");
    }
}

public class ConfirmPutAwayHandler : IRequestHandler<ConfirmPutAwayRequest, ReceivingResult>
{
    private readonly ICommandRepository<GoodsReceive> _receipts;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly ICommandRepository<QualityInspection> _inspections;
    private readonly ICommandRepository<PutAwayRecord> _putAways;
    private readonly ICommandRepository<LocationStock> _stocks;
    private readonly ICommandRepository<StorageLocation> _locations;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmPutAwayHandler(ICommandRepository<GoodsReceive> receipts, ICommandRepository<InventoryTransaction> transactions,
        ICommandRepository<QualityInspection> inspections, ICommandRepository<PutAwayRecord> putAways,
        ICommandRepository<LocationStock> stocks, ICommandRepository<StorageLocation> locations, IUnitOfWork unitOfWork)
    {
        _receipts = receipts;
        _transactions = transactions;
        _inspections = inspections;
        _putAways = putAways;
        _stocks = stocks;
        _locations = locations;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReceivingResult> Handle(ConfirmPutAwayRequest request, CancellationToken ct)
    {
        var receipt = await _receipts.GetAsync(request.GoodsReceiveId ?? string.Empty, ct) ?? throw new Exception("Goods receipt not found.");
        if (receipt.Status != GoodsReceiveStatus.Confirmed) throw new Exception($"{receipt.Number} is not confirmed yet, so its stock has not arrived.");

        var tx = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ModuleId == receipt.Id && x.ModuleName == nameof(GoodsReceive))
            .Select(x => new { x.ProductId, x.WarehouseId, Qty = x.Movement ?? 0 }).ToList();
        var warehouseId = tx.Select(x => x.WarehouseId).FirstOrDefault(w => w != null);
        var received = tx.GroupBy(x => x.ProductId ?? string.Empty).ToDictionary(g => g.Key, g => g.Sum(x => Math.Abs(x.Qty)));
        var inspections = _inspections.GetQuery().ApplyIsDeletedFilter().Where(x => x.GoodsReceiveId == receipt.Id).ToList();
        var done = _putAways.GetQuery().ApplyIsDeletedFilter().Where(x => x.GoodsReceiveId == receipt.Id)
            .Select(x => new { x.ProductId, Qty = x.Quantity ?? 0 }).ToList();

        var lines = (request.Lines ?? new List<PutAwayInput>()).Where(l => l.Quantity > 0).ToList();
        if (lines.Count == 0) throw new Exception("Enter a quantity to put away.");

        foreach (var g in lines.GroupBy(l => l.ProductId ?? string.Empty))
        {
            if (!received.TryGetValue(g.Key, out var got)) throw new Exception("A product is not on this goods receipt.");
            var insp = inspections.FirstOrDefault(i => i.ProductId == g.Key);
            var allowed = insp != null ? Math.Min(got, insp.PassedQuantity ?? 0) : got;
            var placed = done.Where(x => x.ProductId == g.Key).Sum(x => x.Qty);
            var asked = g.Sum(l => l.Quantity);
            if (asked > allowed - placed + 1e-9)
                throw new Exception($"You are putting away {asked:0.##} but only {Math.Max(allowed - placed, 0):0.##} can still be put away" +
                                    (insp != null ? " (units that failed inspection are held back)." : "."));
        }

        var result = new ReceivingResult();
        foreach (var l in lines)
        {
            var location = await _locations.GetAsync(l.LocationId ?? string.Empty, ct) ?? throw new Exception("Location not found.");
            if (!location.IsActive) throw new Exception($"Location {location.Code} is inactive.");
            if (warehouseId != null && location.WarehouseId != warehouseId) throw new Exception($"Location {location.Code} is in a different warehouse from this receipt.");

            var rows = _stocks.GetQuery().ApplyIsDeletedFilter().Where(x => x.LocationId == location.Id).ToList();
            var used = rows.Sum(x => x.Quantity ?? 0);
            if (location.Capacity.HasValue && used + l.Quantity > location.Capacity.Value + 1e-9)
                throw new Exception($"Location {location.Code} has room for {Math.Max(location.Capacity.Value - used, 0):0.##} more units.");

            var mine = rows.FirstOrDefault(x => x.ProductId == l.ProductId);
            if (mine == null)
                await _stocks.CreateAsync(new LocationStock { CreatedById = request.UpdatedById, LocationId = location.Id, ProductId = l.ProductId, Quantity = l.Quantity }, ct);
            else
            {
                mine.UpdatedById = request.UpdatedById;
                mine.Quantity = (mine.Quantity ?? 0) + l.Quantity;
                _stocks.Update(mine);
            }

            await _putAways.CreateAsync(new PutAwayRecord
            {
                CreatedById = request.UpdatedById, GoodsReceiveId = receipt.Id, ProductId = l.ProductId, LocationId = location.Id, Quantity = l.Quantity
            }, ct);
            result.Lines++;
            result.Units += l.Quantity;
        }

        await _unitOfWork.SaveAsync(ct);
        return result;
    }
}
