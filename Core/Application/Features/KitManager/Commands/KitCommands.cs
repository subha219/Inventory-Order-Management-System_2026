using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.InventoryTransactionManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.KitManager.Commands;

public class KitResult
{
    public string? Number { get; set; }
    public string? Message { get; set; }
}

// ---------------------------------------------------------------- bill of materials
public class BomLineInput
{
    public string? ComponentProductId { get; init; }
    public double Quantity { get; init; }
}

public class SaveBomRequest : IRequest<KitResult>
{
    public string? KitProductId { get; init; }
    public List<BomLineInput>? Lines { get; init; }
    public string? UpdatedById { get; init; }
}

public class SaveBomValidator : AbstractValidator<SaveBomRequest>
{
    public SaveBomValidator()
    {
        RuleFor(x => x.KitProductId).NotEmpty().WithMessage("Choose the kit product.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one component.");
        RuleFor(x => x.Lines).Must(l => l == null || l.Count <= 50).WithMessage("A kit can have at most 50 components.");
    }
}

public class SaveBomHandler : IRequestHandler<SaveBomRequest, KitResult>
{
    private readonly ICommandRepository<ProductBomLine> _lines;
    private readonly ICommandRepository<Product> _products;
    private readonly IUnitOfWork _unitOfWork;

    public SaveBomHandler(ICommandRepository<ProductBomLine> lines, ICommandRepository<Product> products, IUnitOfWork unitOfWork)
    {
        _lines = lines; _products = products; _unitOfWork = unitOfWork;
    }

    public async Task<KitResult> Handle(SaveBomRequest request, CancellationToken ct)
    {
        var kit = await _products.GetAsync(request.KitProductId ?? string.Empty, ct) ?? throw new Exception("Kit product not found.");
        if (kit.Physical != true) throw new Exception("A kit must be a physical product.");

        var wanted = (request.Lines ?? new List<BomLineInput>()).Where(l => l.Quantity > 0 && !string.IsNullOrEmpty(l.ComponentProductId)).ToList();
        if (wanted.Count == 0) throw new Exception("Add at least one component with a quantity above zero.");
        if (wanted.Select(l => l.ComponentProductId).Distinct().Count() != wanted.Count) throw new Exception("A component appears twice.");
        if (wanted.Any(l => l.ComponentProductId == kit.Id)) throw new Exception("A kit cannot contain itself.");

        // a component that is itself built from this kit would make a loop
        var all = _lines.GetQuery().ApplyIsDeletedFilter().Where(l => l.KitProductId != kit.Id).Select(l => new { l.KitProductId, l.ComponentProductId }).ToList();
        foreach (var c in wanted)
        {
            if (all.Any(a => a.KitProductId == c.ComponentProductId && a.ComponentProductId == kit.Id))
                throw new Exception("One of the components is itself built from this kit.");
            var comp = await _products.GetAsync(c.ComponentProductId!, ct) ?? throw new Exception("A component product was not found.");
            if (comp.Physical != true) throw new Exception($"{comp.Name} is not a physical product.");
        }

        foreach (var old in _lines.GetQuery().ApplyIsDeletedFilter().Where(l => l.KitProductId == kit.Id).ToList()) _lines.Delete(old);
        foreach (var c in wanted)
            await _lines.CreateAsync(new ProductBomLine { CreatedById = request.UpdatedById, KitProductId = kit.Id, ComponentProductId = c.ComponentProductId, Quantity = c.Quantity }, ct);
        await _unitOfWork.SaveAsync(ct);
        return new KitResult { Message = $"Bill of materials for {kit.Name} saved." };
    }
}

public class DeleteBomRequest : IRequest<KitResult>
{
    public string? KitProductId { get; init; }
    public string? UpdatedById { get; init; }
}

public class DeleteBomHandler : IRequestHandler<DeleteBomRequest, KitResult>
{
    private readonly ICommandRepository<ProductBomLine> _lines;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteBomHandler(ICommandRepository<ProductBomLine> lines, IUnitOfWork unitOfWork)
    {
        _lines = lines; _unitOfWork = unitOfWork;
    }

    public async Task<KitResult> Handle(DeleteBomRequest request, CancellationToken ct)
    {
        var rows = _lines.GetQuery().ApplyIsDeletedFilter().Where(l => l.KitProductId == request.KitProductId).ToList();
        if (rows.Count == 0) throw new Exception("This product has no bill of materials.");
        foreach (var r in rows) { r.UpdatedById = request.UpdatedById; _lines.Delete(r); }
        await _unitOfWork.SaveAsync(ct);
        return new KitResult { Message = "Bill of materials removed." };
    }
}

// ---------------------------------------------------------------- build
public class BuildKitRequest : IRequest<KitResult>
{
    public string? KitProductId { get; init; }
    public string? WarehouseId { get; init; }
    public double Quantity { get; init; }
    public string? Notes { get; init; }
    public string? CreatedById { get; init; }
}

public class BuildKitValidator : AbstractValidator<BuildKitRequest>
{
    public BuildKitValidator()
    {
        RuleFor(x => x.KitProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Choose the warehouse to build in.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be above zero.");
    }
}

/// <summary>
/// Builds kits: the components leave stock on a confirmed negative adjustment and the finished kits enter stock on a
/// confirmed positive adjustment, so everything shows in the normal stock reports and audit trail.
/// </summary>
public class BuildKitHandler : IRequestHandler<BuildKitRequest, KitResult>
{
    private readonly ICommandRepository<ProductBomLine> _lines;
    private readonly ICommandRepository<Product> _products;
    private readonly ICommandRepository<Warehouse> _warehouses;
    private readonly ICommandRepository<NegativeAdjustment> _negative;
    private readonly ICommandRepository<PositiveAdjustment> _positive;
    private readonly ICommandRepository<KitAssembly> _assemblies;
    private readonly InventoryTransactionService _inventory;
    private readonly NumberSequenceService _numbers;
    private readonly IUnitOfWork _unitOfWork;

    public BuildKitHandler(ICommandRepository<ProductBomLine> lines, ICommandRepository<Product> products, ICommandRepository<Warehouse> warehouses,
        ICommandRepository<NegativeAdjustment> negative, ICommandRepository<PositiveAdjustment> positive, ICommandRepository<KitAssembly> assemblies,
        InventoryTransactionService inventory, NumberSequenceService numbers, IUnitOfWork unitOfWork)
    {
        _lines = lines; _products = products; _warehouses = warehouses; _negative = negative; _positive = positive; _assemblies = assemblies;
        _inventory = inventory; _numbers = numbers; _unitOfWork = unitOfWork;
    }

    public async Task<KitResult> Handle(BuildKitRequest request, CancellationToken ct)
    {
        var kit = await _products.GetAsync(request.KitProductId ?? string.Empty, ct) ?? throw new Exception("Kit product not found.");
        var wh = await _warehouses.GetAsync(request.WarehouseId ?? string.Empty, ct) ?? throw new Exception("Warehouse not found.");
        if (wh.SystemWarehouse == true) throw new Exception("Choose a real warehouse.");
        var bom = _lines.GetQuery().ApplyIsDeletedFilter().Where(l => l.KitProductId == kit.Id).ToList();
        if (bom.Count == 0) throw new Exception($"{kit.Name} has no bill of materials yet.");

        var needs = new List<(string Id, string Name, double Qty, double Have)>();
        foreach (var l in bom)
        {
            var comp = await _products.GetAsync(l.ComponentProductId ?? string.Empty, ct) ?? throw new Exception("A component product no longer exists.");
            var qty = (l.Quantity ?? 0) * request.Quantity;
            var have = _inventory.GetStock(wh.Id, comp.Id);
            needs.Add((comp.Id, comp.Name ?? "", qty, have));
        }
        var missing = needs.Where(n => n.Have + 1e-9 < n.Qty).ToList();
        if (missing.Count > 0)
            throw new Exception("Not enough components in " + wh.Name + ": " + string.Join("; ", missing.Select(m => $"{m.Name} needs {m.Qty:0.##}, has {m.Have:0.##}")) + ".");

        var number = _numbers.GenerateNumber(nameof(KitAssembly), "", "KIT");
        var today = DateTime.Today;

        var neg = new NegativeAdjustment
        {
            CreatedById = request.CreatedById, Number = _numbers.GenerateNumber(nameof(NegativeAdjustment), "", "ADJ-"), AdjustmentDate = today,
            Status = AdjustmentStatus.Draft, Description = $"Kit build {number}: components for {request.Quantity:0.##} x {kit.Name}"
        };
        await _negative.CreateAsync(neg, ct);
        var pos = new PositiveAdjustment
        {
            CreatedById = request.CreatedById, Number = _numbers.GenerateNumber(nameof(PositiveAdjustment), "", "ADJ+"), AdjustmentDate = today,
            Status = AdjustmentStatus.Draft, Description = $"Kit build {number}: {request.Quantity:0.##} x {kit.Name}"
        };
        await _positive.CreateAsync(pos, ct);
        await _unitOfWork.SaveAsync(ct);

        foreach (var n in needs) await _inventory.NegativeAdjustmentCreateInvenTrans(neg.Id, wh.Id, n.Id, n.Qty, request.CreatedById, ct);
        await _inventory.PositiveAdjustmentCreateInvenTrans(pos.Id, wh.Id, kit.Id, request.Quantity, request.CreatedById, ct);

        neg.Status = AdjustmentStatus.Confirmed; neg.UpdatedById = request.CreatedById; _negative.Update(neg);
        pos.Status = AdjustmentStatus.Confirmed; pos.UpdatedById = request.CreatedById; _positive.Update(pos);
        await _unitOfWork.SaveAsync(ct);
        await _inventory.PropagateParentUpdate(neg.Id, nameof(NegativeAdjustment), neg.AdjustmentDate, (InventoryTransactionStatus?)neg.Status, neg.IsDeleted, neg.UpdatedById, null, ct);
        await _inventory.PropagateParentUpdate(pos.Id, nameof(PositiveAdjustment), pos.AdjustmentDate, (InventoryTransactionStatus?)pos.Status, pos.IsDeleted, pos.UpdatedById, null, ct);

        await _assemblies.CreateAsync(new KitAssembly
        {
            CreatedById = request.CreatedById, Number = number, KitProductId = kit.Id, WarehouseId = wh.Id, Quantity = request.Quantity,
            ComponentsDocument = neg.Number, KitsDocument = pos.Number, Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        }, ct);
        await _unitOfWork.SaveAsync(ct);
        return new KitResult { Number = number, Message = $"Built {request.Quantity:0.##} x {kit.Name} ({number}). Components left stock on {neg.Number}; the kits arrived on {pos.Number}." };
    }
}
