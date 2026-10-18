using Application.Common.Extensions;
using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.StorageLocationManager.Commands;

public class StorageLocationResult
{
    public string? Id { get; set; }
    public int Count { get; set; }
    public string? Message { get; set; }
}

// ---------------------------------------------------------------- create
public class CreateStorageLocationRequest : IRequest<StorageLocationResult>
{
    public string? WarehouseId { get; init; }
    public string? Code { get; init; }
    public string? Zone { get; init; }
    public string? Aisle { get; init; }
    public string? Rack { get; init; }
    public string? Shelf { get; init; }
    public string? Bin { get; init; }
    public string? LocationType { get; init; }
    public double? Capacity { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateStorageLocationValidator : AbstractValidator<CreateStorageLocationRequest>
{
    public CreateStorageLocationValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity.HasValue);
    }
}

public class CreateStorageLocationHandler : IRequestHandler<CreateStorageLocationRequest, StorageLocationResult>
{
    private readonly ICommandRepository<StorageLocation> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateStorageLocationHandler(ICommandRepository<StorageLocation> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<StorageLocationResult> Handle(CreateStorageLocationRequest request, CancellationToken ct)
    {
        var code = request.Code!.Trim();
        if (_repository.GetQuery().ApplyIsDeletedFilter().Any(x => x.WarehouseId == request.WarehouseId && x.Code == code))
            throw new Exception($"Location {code} already exists in this warehouse.");

        var entity = new StorageLocation
        {
            CreatedById = request.CreatedById,
            WarehouseId = request.WarehouseId,
            Code = code,
            Zone = request.Zone?.Trim(),
            Aisle = request.Aisle?.Trim(),
            Rack = request.Rack?.Trim(),
            Shelf = request.Shelf?.Trim(),
            Bin = request.Bin?.Trim(),
            LocationType = string.IsNullOrWhiteSpace(request.LocationType) ? "Bin" : request.LocationType,
            Capacity = request.Capacity,
            IsActive = request.IsActive,
            Notes = request.Notes
        };
        await _repository.CreateAsync(entity, ct);
        await _unitOfWork.SaveAsync(ct);
        return new StorageLocationResult { Id = entity.Id, Count = 1 };
    }
}

// ---------------------------------------------------------------- update
public class UpdateStorageLocationRequest : IRequest<StorageLocationResult>
{
    public string? Id { get; init; }
    public string? Code { get; init; }
    public string? Zone { get; init; }
    public string? Aisle { get; init; }
    public string? Rack { get; init; }
    public string? Shelf { get; init; }
    public string? Bin { get; init; }
    public string? LocationType { get; init; }
    public double? Capacity { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdateStorageLocationValidator : AbstractValidator<UpdateStorageLocationRequest>
{
    public UpdateStorageLocationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity.HasValue);
    }
}

public class UpdateStorageLocationHandler : IRequestHandler<UpdateStorageLocationRequest, StorageLocationResult>
{
    private readonly ICommandRepository<StorageLocation> _repository;
    private readonly ICommandRepository<LocationStock> _stockRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStorageLocationHandler(ICommandRepository<StorageLocation> repository, ICommandRepository<LocationStock> stockRepository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _stockRepository = stockRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<StorageLocationResult> Handle(UpdateStorageLocationRequest request, CancellationToken ct)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, ct);
        if (entity == null) throw new Exception($"Entity not found: {request.Id}");

        var code = request.Code!.Trim();
        if (_repository.GetQuery().ApplyIsDeletedFilter().Any(x => x.WarehouseId == entity.WarehouseId && x.Code == code && x.Id != entity.Id))
            throw new Exception($"Location {code} already exists in this warehouse.");

        if (request.Capacity.HasValue)
        {
            var used = _stockRepository.GetQuery().ApplyIsDeletedFilter().Where(x => x.LocationId == entity.Id).Sum(x => x.Quantity ?? 0);
            if (used > request.Capacity.Value)
                throw new Exception($"This location already holds {used:0.##} units, which is more than the new capacity.");
        }

        entity.UpdatedById = request.UpdatedById;
        entity.Code = code;
        entity.Zone = request.Zone?.Trim();
        entity.Aisle = request.Aisle?.Trim();
        entity.Rack = request.Rack?.Trim();
        entity.Shelf = request.Shelf?.Trim();
        entity.Bin = request.Bin?.Trim();
        entity.LocationType = string.IsNullOrWhiteSpace(request.LocationType) ? "Bin" : request.LocationType;
        entity.Capacity = request.Capacity;
        entity.IsActive = request.IsActive;
        entity.Notes = request.Notes;

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(ct);
        return new StorageLocationResult { Id = entity.Id, Count = 1 };
    }
}

// ---------------------------------------------------------------- delete
public class DeleteStorageLocationRequest : IRequest<StorageLocationResult>
{
    public string? Id { get; init; }
    public string? DeletedById { get; init; }
}

public class DeleteStorageLocationValidator : AbstractValidator<DeleteStorageLocationRequest>
{
    public DeleteStorageLocationValidator() { RuleFor(x => x.Id).NotEmpty(); }
}

public class DeleteStorageLocationHandler : IRequestHandler<DeleteStorageLocationRequest, StorageLocationResult>
{
    private readonly ICommandRepository<StorageLocation> _repository;
    private readonly ICommandRepository<LocationStock> _stockRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteStorageLocationHandler(ICommandRepository<StorageLocation> repository, ICommandRepository<LocationStock> stockRepository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _stockRepository = stockRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<StorageLocationResult> Handle(DeleteStorageLocationRequest request, CancellationToken ct)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, ct);
        if (entity == null) throw new Exception($"Entity not found: {request.Id}");

        var used = _stockRepository.GetQuery().ApplyIsDeletedFilter().Where(x => x.LocationId == entity.Id).Sum(x => x.Quantity ?? 0);
        if (used > 0) throw new Exception($"Location {entity.Code} still holds {used:0.##} units. Move them out first.");

        entity.UpdatedById = request.DeletedById;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(ct);
        return new StorageLocationResult { Id = entity.Id, Count = 1 };
    }
}

// ---------------------------------------------------------------- generate
public class GenerateStorageLocationsRequest : IRequest<StorageLocationResult>
{
    public string? WarehouseId { get; init; }
    public string? Zone { get; init; }
    public int Aisles { get; init; } = 1;
    public int RacksPerAisle { get; init; } = 1;
    public int ShelvesPerRack { get; init; } = 1;
    public int BinsPerShelf { get; init; } = 1;
    public double? Capacity { get; init; }
    public string? CreatedById { get; init; }
}

public class GenerateStorageLocationsValidator : AbstractValidator<GenerateStorageLocationsRequest>
{
    public GenerateStorageLocationsValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Zone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Aisles).InclusiveBetween(1, 50);
        RuleFor(x => x.RacksPerAisle).InclusiveBetween(1, 50);
        RuleFor(x => x.ShelvesPerRack).InclusiveBetween(1, 10);
        RuleFor(x => x.BinsPerShelf).InclusiveBetween(1, 20);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity.HasValue);
    }
}

public class GenerateStorageLocationsHandler : IRequestHandler<GenerateStorageLocationsRequest, StorageLocationResult>
{
    private const int MaxPerRun = 2000;
    private readonly ICommandRepository<StorageLocation> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateStorageLocationsHandler(ICommandRepository<StorageLocation> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<StorageLocationResult> Handle(GenerateStorageLocationsRequest request, CancellationToken ct)
    {
        var total = request.Aisles * request.RacksPerAisle * request.ShelvesPerRack * request.BinsPerShelf;
        if (total > MaxPerRun) throw new Exception($"That would create {total} locations. Please keep each run to {MaxPerRun} or fewer.");

        var zone = request.Zone!.Trim().ToUpperInvariant();
        var existing = _repository.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.WarehouseId == request.WarehouseId).Select(x => x.Code).ToList()
            .Where(c => c != null).Select(c => c!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = 0;
        for (var a = 1; a <= request.Aisles; a++)
            for (var r = 1; r <= request.RacksPerAisle; r++)
                for (var s = 1; s <= request.ShelvesPerRack; s++)
                    for (var b = 1; b <= request.BinsPerShelf; b++)
                    {
                        var code = $"{zone}-{a:00}-{r:00}-{s}-{b:00}";
                        if (existing.Contains(code)) continue;
                        await _repository.CreateAsync(new StorageLocation
                        {
                            CreatedById = request.CreatedById,
                            WarehouseId = request.WarehouseId,
                            Code = code,
                            Zone = zone,
                            Aisle = a.ToString("00"),
                            Rack = r.ToString("00"),
                            Shelf = s.ToString(),
                            Bin = b.ToString("00"),
                            LocationType = "Bin",
                            Capacity = request.Capacity,
                            IsActive = true
                        }, ct);
                        created++;
                    }

        if (created > 0) await _unitOfWork.SaveAsync(ct);
        return new StorageLocationResult
        {
            Count = created,
            Message = created == total ? $"Created {created} locations." : $"Created {created} locations ({total - created} already existed)."
        };
    }
}

// ---------------------------------------------------------------- put-away / set quantity
public class SetLocationStockRequest : IRequest<StorageLocationResult>
{
    public string? LocationId { get; init; }
    public string? ProductId { get; init; }
    /// <summary>The new total quantity of this product in the location (0 removes it).</summary>
    public double Quantity { get; init; }
    public string? UpdatedById { get; init; }
}

public class SetLocationStockValidator : AbstractValidator<SetLocationStockRequest>
{
    public SetLocationStockValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
    }
}

public class SetLocationStockHandler : IRequestHandler<SetLocationStockRequest, StorageLocationResult>
{
    private readonly ICommandRepository<StorageLocation> _locations;
    private readonly ICommandRepository<LocationStock> _stocks;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly IUnitOfWork _unitOfWork;

    public SetLocationStockHandler(ICommandRepository<StorageLocation> locations, ICommandRepository<LocationStock> stocks,
        ICommandRepository<InventoryTransaction> transactions, IUnitOfWork unitOfWork)
    {
        _locations = locations;
        _stocks = stocks;
        _transactions = transactions;
        _unitOfWork = unitOfWork;
    }

    public async Task<StorageLocationResult> Handle(SetLocationStockRequest request, CancellationToken ct)
    {
        var location = await _locations.GetAsync(request.LocationId ?? string.Empty, ct);
        if (location == null) throw new Exception("Location not found.");
        if (!location.IsActive && request.Quantity > 0) throw new Exception($"Location {location.Code} is inactive.");

        var rows = _stocks.GetQuery().ApplyIsDeletedFilter().Where(x => x.LocationId == location.Id).ToList();
        var mine = rows.FirstOrDefault(x => x.ProductId == request.ProductId);
        var others = rows.Where(x => x.ProductId != request.ProductId).Sum(x => x.Quantity ?? 0);

        if (location.Capacity.HasValue && others + request.Quantity > location.Capacity.Value + 1e-9)
            throw new Exception($"Location {location.Code} can hold {location.Capacity.Value:0.##} units; this would make {others + request.Quantity:0.##}.");

        // You cannot put away more of a product than the warehouse actually has on hand.
        var onHand = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.WarehouseId == location.WarehouseId && x.ProductId == request.ProductId && x.Status == InventoryTransactionStatus.Confirmed)
            .Sum(x => x.Stock ?? 0);
        var placedElsewhere = _stocks.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ProductId == request.ProductId && x.LocationId != location.Id && x.Location!.WarehouseId == location.WarehouseId)
            .Sum(x => x.Quantity ?? 0);
        if (request.Quantity + placedElsewhere > onHand + 1e-9)
            throw new Exception($"The warehouse only has {onHand:0.##} units on hand and {placedElsewhere:0.##} are already placed in other locations.");

        if (request.Quantity <= 0)
        {
            if (mine != null) { mine.UpdatedById = request.UpdatedById; _stocks.Delete(mine); }
        }
        else if (mine == null)
        {
            await _stocks.CreateAsync(new LocationStock
            {
                CreatedById = request.UpdatedById, LocationId = location.Id, ProductId = request.ProductId, Quantity = request.Quantity
            }, ct);
        }
        else
        {
            mine.UpdatedById = request.UpdatedById;
            mine.Quantity = request.Quantity;
            _stocks.Update(mine);
        }

        await _unitOfWork.SaveAsync(ct);
        return new StorageLocationResult { Id = location.Id, Count = 1 };
    }
}

// ---------------------------------------------------------------- bin to bin move
public class MoveLocationStockRequest : IRequest<StorageLocationResult>
{
    public string? FromLocationId { get; init; }
    public string? ToLocationId { get; init; }
    public string? ProductId { get; init; }
    public double Quantity { get; init; }
    public string? UpdatedById { get; init; }
}

public class MoveLocationStockValidator : AbstractValidator<MoveLocationStockRequest>
{
    public MoveLocationStockValidator()
    {
        RuleFor(x => x.FromLocationId).NotEmpty();
        RuleFor(x => x.ToLocationId).NotEmpty().NotEqual(x => x.FromLocationId).WithMessage("Choose a different destination.");
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public class MoveLocationStockHandler : IRequestHandler<MoveLocationStockRequest, StorageLocationResult>
{
    private readonly ICommandRepository<StorageLocation> _locations;
    private readonly ICommandRepository<LocationStock> _stocks;
    private readonly IUnitOfWork _unitOfWork;

    public MoveLocationStockHandler(ICommandRepository<StorageLocation> locations, ICommandRepository<LocationStock> stocks, IUnitOfWork unitOfWork)
    {
        _locations = locations;
        _stocks = stocks;
        _unitOfWork = unitOfWork;
    }

    public async Task<StorageLocationResult> Handle(MoveLocationStockRequest request, CancellationToken ct)
    {
        var from = await _locations.GetAsync(request.FromLocationId ?? string.Empty, ct);
        var to = await _locations.GetAsync(request.ToLocationId ?? string.Empty, ct);
        if (from == null || to == null) throw new Exception("Location not found.");
        if (from.WarehouseId != to.WarehouseId) throw new Exception("Bin-to-bin moves stay inside one warehouse. Use a warehouse transfer to move stock between warehouses.");
        if (!to.IsActive) throw new Exception($"Location {to.Code} is inactive.");

        var source = _stocks.GetQuery().ApplyIsDeletedFilter().FirstOrDefault(x => x.LocationId == from.Id && x.ProductId == request.ProductId);
        if (source == null || (source.Quantity ?? 0) + 1e-9 < request.Quantity)
            throw new Exception($"{from.Code} holds only {(source?.Quantity ?? 0):0.##} units of this product.");

        var targetRows = _stocks.GetQuery().ApplyIsDeletedFilter().Where(x => x.LocationId == to.Id).ToList();
        var targetUsed = targetRows.Sum(x => x.Quantity ?? 0);
        if (to.Capacity.HasValue && targetUsed + request.Quantity > to.Capacity.Value + 1e-9)
            throw new Exception($"{to.Code} has room for {Math.Max(to.Capacity.Value - targetUsed, 0):0.##} more units.");

        var remaining = (source.Quantity ?? 0) - request.Quantity;
        source.UpdatedById = request.UpdatedById;
        if (remaining <= 1e-9) _stocks.Delete(source);
        else { source.Quantity = remaining; _stocks.Update(source); }

        var target = targetRows.FirstOrDefault(x => x.ProductId == request.ProductId);
        if (target == null)
            await _stocks.CreateAsync(new LocationStock { CreatedById = request.UpdatedById, LocationId = to.Id, ProductId = request.ProductId, Quantity = request.Quantity }, ct);
        else
        {
            target.UpdatedById = request.UpdatedById;
            target.Quantity = (target.Quantity ?? 0) + request.Quantity;
            _stocks.Update(target);
        }

        await _unitOfWork.SaveAsync(ct);
        return new StorageLocationResult { Count = 1 };
    }
}
