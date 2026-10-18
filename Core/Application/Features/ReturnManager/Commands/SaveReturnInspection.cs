using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.InventoryTransactionManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.ReturnManager.Commands;

public class ReturnInspectionInput
{
    public string? ProductId { get; init; }
    public double Good { get; init; }
    public double Damaged { get; init; }
    public string? ReasonCode { get; init; }
    public string? Note { get; init; }
    public string? LocationId { get; init; }
}

public class SaveReturnInspectionRequest : IRequest<SaveReturnInspectionResult>
{
    public string? SalesReturnId { get; init; }
    public List<ReturnInspectionInput>? Lines { get; init; }
    public string? UpdatedById { get; init; }
}

public class SaveReturnInspectionResult
{
    public double GoodUnits { get; set; }
    public double DamagedUnits { get; set; }
    public string? ScrapNumber { get; set; }
    public string? Note { get; set; }
}

public class SaveReturnInspectionValidator : AbstractValidator<SaveReturnInspectionRequest>
{
    public SaveReturnInspectionValidator()
    {
        RuleFor(x => x.SalesReturnId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Enter the result for at least one product.");
    }
}

/// <summary>
/// Records what came back on a customer return. Good units can be put into a storage location; damaged units go on ONE draft
/// scrapping document per return, so nothing leaves stock until a person confirms it. Saving again corrects the earlier result.
/// </summary>
public class SaveReturnInspectionHandler : IRequestHandler<SaveReturnInspectionRequest, SaveReturnInspectionResult>
{
    private readonly ICommandRepository<SalesReturn> _returns;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly ICommandRepository<ReturnInspection> _inspections;
    private readonly ICommandRepository<LocationStock> _stocks;
    private readonly ICommandRepository<StorageLocation> _locations;
    private readonly ICommandRepository<Scrapping> _scrappings;
    private readonly NumberSequenceService _numbers;
    private readonly InventoryTransactionService _inventory;
    private readonly IUnitOfWork _unitOfWork;

    public SaveReturnInspectionHandler(ICommandRepository<SalesReturn> returns, ICommandRepository<InventoryTransaction> transactions,
        ICommandRepository<ReturnInspection> inspections, ICommandRepository<LocationStock> stocks, ICommandRepository<StorageLocation> locations,
        ICommandRepository<Scrapping> scrappings, NumberSequenceService numbers, InventoryTransactionService inventory, IUnitOfWork unitOfWork)
    {
        _returns = returns; _transactions = transactions; _inspections = inspections; _stocks = stocks; _locations = locations;
        _scrappings = scrappings; _numbers = numbers; _inventory = inventory; _unitOfWork = unitOfWork;
    }

    public async Task<SaveReturnInspectionResult> Handle(SaveReturnInspectionRequest request, CancellationToken ct)
    {
        var ret = await _returns.GetAsync(request.SalesReturnId ?? string.Empty, ct) ?? throw new Exception("Return not found.");
        if (ret.Status != SalesReturnStatus.Confirmed) throw new Exception($"{ret.Number} is not confirmed yet, so the goods have not been received back.");

        var tx = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ModuleId == ret.Id && x.ModuleName == nameof(SalesReturn))
            .Select(x => new { x.ProductId, x.WarehouseId, Qty = x.Movement ?? 0 }).ToList();
        var warehouseId = tx.Select(x => x.WarehouseId).FirstOrDefault(w => w != null);
        var returned = tx.GroupBy(x => x.ProductId ?? "").ToDictionary(g => g.Key, g => g.Sum(x => Math.Abs(x.Qty)));
        var existing = _inspections.GetQuery().ApplyIsDeletedFilter().Where(x => x.SalesReturnId == ret.Id).ToList();

        var result = new SaveReturnInspectionResult();
        foreach (var l in request.Lines ?? new List<ReturnInspectionInput>())
        {
            var pid = l.ProductId ?? string.Empty;
            if (!returned.TryGetValue(pid, out var got)) throw new Exception("A product is not on this return.");
            if (l.Good < 0 || l.Damaged < 0) throw new Exception("Quantities cannot be negative.");
            if (l.Good + l.Damaged > got + 1e-9) throw new Exception($"Good plus damaged ({l.Good + l.Damaged:0.##}) is more than the {got:0.##} returned.");
            var reason = ReturnReasons.All.Contains(l.ReasonCode ?? "") ? l.ReasonCode : (string.IsNullOrWhiteSpace(l.ReasonCode) ? null : "Other");

            StorageLocation? location = null;
            if (l.Good > 0 && !string.IsNullOrEmpty(l.LocationId))
            {
                location = await _locations.GetAsync(l.LocationId, ct) ?? throw new Exception("Location not found.");
                if (!location.IsActive) throw new Exception($"Location {location.Code} is inactive.");
                if (warehouseId != null && location.WarehouseId != warehouseId) throw new Exception($"Location {location.Code} is in a different warehouse from this return.");
            }

            var row = existing.FirstOrDefault(x => x.ProductId == pid);
            // take back an earlier placement before applying the new one
            if (row != null && (row.GoodQuantity ?? 0) > 0 && !string.IsNullOrEmpty(row.LocationId))
            {
                var prev = _stocks.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(x => x.LocationId == row.LocationId && x.ProductId == pid);
                if (prev != null) { prev.Quantity = Math.Max((prev.Quantity ?? 0) - (row.GoodQuantity ?? 0), 0); prev.UpdatedById = request.UpdatedById; _stocks.Update(prev); }
            }
            if (location != null)
            {
                var rows = _stocks.GetQuery().ApplyIsDeletedFilter().Where(x => x.LocationId == location.Id).ToList();
                var used = rows.Sum(x => x.Quantity ?? 0);
                if (location.Capacity.HasValue && used + l.Good > location.Capacity.Value + 1e-9)
                    throw new Exception($"Location {location.Code} has room for {Math.Max(location.Capacity.Value - used, 0):0.##} more units.");
                var mine = rows.FirstOrDefault(x => x.ProductId == pid);
                if (mine == null) await _stocks.CreateAsync(new LocationStock { CreatedById = request.UpdatedById, LocationId = location.Id, ProductId = pid, Quantity = l.Good }, ct);
                else { mine.Quantity = (mine.Quantity ?? 0) + l.Good; mine.UpdatedById = request.UpdatedById; _stocks.Update(mine); }
            }

            if (row == null)
                await _inspections.CreateAsync(new ReturnInspection
                {
                    CreatedById = request.UpdatedById, SalesReturnId = ret.Id, ProductId = pid, ReturnedQuantity = got, GoodQuantity = l.Good, DamagedQuantity = l.Damaged,
                    ReasonCode = reason, Note = l.Note, LocationId = location?.Id
                }, ct);
            else
            {
                row.UpdatedById = request.UpdatedById; row.ReturnedQuantity = got; row.GoodQuantity = l.Good; row.DamagedQuantity = l.Damaged;
                row.ReasonCode = reason; row.Note = l.Note; row.LocationId = location?.Id;
                _inspections.Update(row);
            }
            result.GoodUnits += l.Good; result.DamagedUnits += l.Damaged;
        }
        await _unitOfWork.SaveAsync(ct);

        await SyncScrapDraft(ret, warehouseId, request.UpdatedById, result, ct);
        return result;
    }

    private async Task SyncScrapDraft(SalesReturn ret, string? warehouseId, string? userId, SaveReturnInspectionResult result, CancellationToken ct)
    {
        var tag = $"Returned damaged - {ret.Number}";
        var damaged = _inspections.GetQuery().ApplyIsDeletedFilter().Where(x => x.SalesReturnId == ret.Id && (x.DamagedQuantity ?? 0) > 0)
            .Select(x => new { x.ProductId, Qty = x.DamagedQuantity ?? 0 }).ToList()
            .Where(x => x.ProductId != null).ToDictionary(x => x.ProductId!, x => x.Qty);

        var scrap = _scrappings.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(x => x.Description == tag);
        if (scrap == null && damaged.Count == 0) return;
        if (scrap != null && scrap.Status != ScrappingStatus.Draft)
        {
            result.ScrapNumber = scrap.Number;
            result.Note = $"{scrap.Number} is already {scrap.Status}, so it was left as it is.";
            return;
        }
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
            if (damaged.TryGetValue(pid, out var qty))
            {
                if (Math.Abs((line.Movement ?? 0) - qty) > 1e-9) await _inventory.ScrappingUpdateInvenTrans(line.Id, pid, qty, userId, ct);
                damaged.Remove(pid);
            }
            else await _inventory.ScrappingDeleteInvenTrans(line.Id, userId, ct);
        }
        foreach (var (pid, qty) in damaged) await _inventory.ScrappingCreateInvenTrans(scrap.Id, pid, qty, userId, ct);

        var total = (await _inventory.ScrappingGetInvenTransList(scrap.Id, nameof(Scrapping), ct)).Sum(x => x.Movement ?? 0);
        result.ScrapNumber = scrap.Number;
        result.Note = total > 0
            ? $"Draft scrapping {scrap.Number} holds {total:0.##} damaged unit(s). Confirm it on the Scrapping page to write them off."
            : $"{scrap.Number} has no damaged units any more. You can delete or cancel it on the Scrapping page.";
    }
}
