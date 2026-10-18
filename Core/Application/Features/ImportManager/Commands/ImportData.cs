using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.InventoryTransactionManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.ImportManager.Commands;

public class ImportRowResult
{
    public int Row { get; set; }
    /// <summary>Ok, Skipped or Error.</summary>
    public string Status { get; set; } = "Ok";
    public string? Message { get; set; }
}

public class ImportResult
{
    public bool DryRun { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public string? Reference { get; set; }
    public List<ImportRowResult> Rows { get; set; } = new();
}

// ---------------------------------------------------------------- products
public class ProductImportRow
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? UnitPrice { get; init; }
    public string? ReorderPoint { get; init; }
    public string? ReorderQuantity { get; init; }
    public string? ProductGroup { get; init; }
    public string? UnitMeasure { get; init; }
    public string? Physical { get; init; }
}

public class ImportProductsRequest : IRequest<ImportResult>
{
    public List<ProductImportRow>? Rows { get; init; }
    public bool DryRun { get; init; }
    public string? CreatedById { get; init; }
}

public class ImportProductsHandler : IRequestHandler<ImportProductsRequest, ImportResult>
{
    private readonly ICommandRepository<Product> _products;
    private readonly ICommandRepository<ProductGroup> _groups;
    private readonly ICommandRepository<UnitMeasure> _units;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;

    public ImportProductsHandler(ICommandRepository<Product> products, ICommandRepository<ProductGroup> groups,
        ICommandRepository<UnitMeasure> units, IUnitOfWork unitOfWork, NumberSequenceService numbers)
    {
        _products = products;
        _groups = groups;
        _units = units;
        _unitOfWork = unitOfWork;
        _numbers = numbers;
    }

    internal static bool TryNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var t = text.Trim().Replace(",", "");
        return double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    public async Task<ImportResult> Handle(ImportProductsRequest request, CancellationToken ct)
    {
        var rows = request.Rows ?? new List<ProductImportRow>();
        if (rows.Count == 0) throw new Exception("The file has no rows.");
        if (rows.Count > 3000) throw new Exception("Import at most 3000 rows at a time.");

        var result = new ImportResult { DryRun = request.DryRun };
        var existingNames = new HashSet<string>(
            _products.GetQuery().ApplyIsDeletedFilter().Select(p => p.Name!).ToList().Where(n => n != null), StringComparer.OrdinalIgnoreCase);
        var groups = _groups.GetQuery().ApplyIsDeletedFilter().ToList();
        var units = _units.GetQuery().ApplyIsDeletedFilter().ToList();
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var res = new ImportRowResult { Row = i + 2 }; // +2: one for the header row, one because people count from 1
            result.Rows.Add(res);

            var name = (r.Name ?? string.Empty).Trim();
            if (name.Length == 0) { Fail(res, result, "Name is empty."); continue; }
            if (name.Length > 255) { Fail(res, result, "Name is longer than 255 characters."); continue; }
            if (existingNames.Contains(name) || !seenInFile.Add(name)) { res.Status = "Skipped"; res.Message = "A product with this name already exists."; result.Skipped++; continue; }
            if (!TryNumber(r.UnitPrice, out var price) || price < 0) { Fail(res, result, "Unit price is not a valid number."); continue; }
            if (!TryNumber(r.ReorderPoint, out var rp) || rp < 0) { Fail(res, result, "Reorder point is not a valid number."); continue; }
            if (!TryNumber(r.ReorderQuantity, out var rq) || rq < 0) { Fail(res, result, "Reorder quantity is not a valid number."); continue; }
            var physical = !(r.Physical ?? "").Trim().ToLowerInvariant().StartsWith("n") && !(r.Physical ?? "").Trim().ToLowerInvariant().StartsWith("f");

            var groupName = string.IsNullOrWhiteSpace(r.ProductGroup) ? "General" : r.ProductGroup.Trim();
            var unitName = string.IsNullOrWhiteSpace(r.UnitMeasure) ? "Pcs" : r.UnitMeasure.Trim();
            var group = groups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));
            var unit = units.FirstOrDefault(u => string.Equals(u.Name, unitName, StringComparison.OrdinalIgnoreCase));
            var notes = new List<string>();

            if (!request.DryRun)
            {
                if (group == null)
                {
                    group = new ProductGroup { CreatedById = request.CreatedById, Name = groupName };
                    await _groups.CreateAsync(group, ct);
                    groups.Add(group);
                }
                if (unit == null)
                {
                    unit = new UnitMeasure { CreatedById = request.CreatedById, Name = unitName };
                    await _units.CreateAsync(unit, ct);
                    units.Add(unit);
                }
                await _products.CreateAsync(new Product
                {
                    CreatedById = request.CreatedById, Number = _numbers.GenerateNumber(nameof(Product), "", "ART"), Name = name,
                    Description = r.Description, UnitPrice = price, ReorderPoint = rp, ReorderQuantity = rq, Physical = physical,
                    ProductGroupId = group.Id, UnitMeasureId = unit.Id
                }, ct);
            }
            else
            {
                if (group == null) notes.Add($"group \"{groupName}\" will be created");
                if (unit == null) notes.Add($"unit \"{unitName}\" will be created");
            }
            res.Message = notes.Count == 0 ? null : string.Join(", ", notes);
            result.Created++;
        }

        if (!request.DryRun && result.Created > 0) await _unitOfWork.SaveAsync(ct);
        return result;
    }

    private static void Fail(ImportRowResult res, ImportResult result, string message)
    {
        res.Status = "Error";
        res.Message = message;
        result.Errors++;
    }
}

// ---------------------------------------------------------------- opening stock
public class StockImportRow
{
    /// <summary>Product number (ART...) or exact product name.</summary>
    public string? Product { get; init; }
    public string? Quantity { get; init; }
}

public class ImportOpeningStockRequest : IRequest<ImportResult>
{
    public string? WarehouseId { get; init; }
    public DateTime? Date { get; init; }
    public string? Description { get; init; }
    public List<StockImportRow>? Rows { get; init; }
    public bool DryRun { get; init; }
    public string? CreatedById { get; init; }
}

/// <summary>Turns a stock list into one Draft positive adjustment, so the usual review and approval still apply before stock moves.</summary>
public class ImportOpeningStockHandler : IRequestHandler<ImportOpeningStockRequest, ImportResult>
{
    private readonly ICommandRepository<PositiveAdjustment> _adjustments;
    private readonly ICommandRepository<Product> _products;
    private readonly ICommandRepository<Warehouse> _warehouses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;
    private readonly InventoryTransactionService _inventory;

    public ImportOpeningStockHandler(ICommandRepository<PositiveAdjustment> adjustments, ICommandRepository<Product> products,
        ICommandRepository<Warehouse> warehouses, IUnitOfWork unitOfWork, NumberSequenceService numbers, InventoryTransactionService inventory)
    {
        _adjustments = adjustments;
        _products = products;
        _warehouses = warehouses;
        _unitOfWork = unitOfWork;
        _numbers = numbers;
        _inventory = inventory;
    }

    public async Task<ImportResult> Handle(ImportOpeningStockRequest request, CancellationToken ct)
    {
        var rows = request.Rows ?? new List<StockImportRow>();
        if (rows.Count == 0) throw new Exception("The file has no rows.");
        if (rows.Count > 3000) throw new Exception("Import at most 3000 rows at a time.");
        var warehouse = await _warehouses.GetAsync(request.WarehouseId ?? string.Empty, ct) ?? throw new Exception("Choose a warehouse.");

        var products = _products.GetQuery().ApplyIsDeletedFilter().Where(p => p.Physical == true).ToList();
        var result = new ImportResult { DryRun = request.DryRun };
        var good = new List<(Product product, double qty)>();
        var seen = new HashSet<string>();

        for (var i = 0; i < rows.Count; i++)
        {
            var res = new ImportRowResult { Row = i + 2 };
            result.Rows.Add(res);
            var key = (rows[i].Product ?? string.Empty).Trim();
            if (key.Length == 0) { res.Status = "Error"; res.Message = "Product is empty."; result.Errors++; continue; }
            var p = products.FirstOrDefault(x => string.Equals(x.Number, key, StringComparison.OrdinalIgnoreCase))
                    ?? products.FirstOrDefault(x => string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase));
            if (p == null) { res.Status = "Error"; res.Message = $"No physical product matches \"{key}\"."; result.Errors++; continue; }
            if (!ImportProductsHandler.TryNumber(rows[i].Quantity, out var qty) || qty <= 0) { res.Status = "Error"; res.Message = "Quantity must be a number above zero."; result.Errors++; continue; }
            if (!seen.Add(p.Id)) { res.Status = "Error"; res.Message = $"{p.Name} appears twice in the file."; result.Errors++; continue; }
            good.Add((p, qty));
            result.Created++;
        }

        if (request.DryRun || good.Count == 0) return result;

        var adj = new PositiveAdjustment
        {
            CreatedById = request.CreatedById,
            Number = _numbers.GenerateNumber(nameof(PositiveAdjustment), "", "ADJ+"),
            AdjustmentDate = request.Date ?? DateTime.Today,
            Status = AdjustmentStatus.Draft,
            Description = string.IsNullOrWhiteSpace(request.Description) ? $"Opening stock import for {warehouse.Name}" : request.Description
        };
        await _adjustments.CreateAsync(adj, ct);
        await _unitOfWork.SaveAsync(ct);

        foreach (var (product, qty) in good)
            await _inventory.PositiveAdjustmentCreateInvenTrans(adj.Id, warehouse.Id, product.Id, qty, request.CreatedById, ct);

        result.Reference = adj.Number;
        return result;
    }
}
