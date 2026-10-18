using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Application.Features.PickListManager.Commands;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.WaveManager.Commands;

public class WaveResult
{
    public string? Id { get; set; }
    public string? Number { get; set; }
    public int Orders { get; set; }
    public double Units { get; set; }
    public bool Done { get; set; }
}

// ---------------------------------------------------------------- create
public class CreateWaveRequest : IRequest<WaveResult>
{
    public List<string>? DeliveryOrderIds { get; init; }
    public string? Notes { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateWaveValidator : AbstractValidator<CreateWaveRequest>
{
    public CreateWaveValidator()
    {
        RuleFor(x => x.DeliveryOrderIds).NotEmpty().WithMessage("Choose at least one delivery order.");
        RuleFor(x => x.DeliveryOrderIds).Must(l => l == null || l.Count <= 40).WithMessage("A wave can hold at most 40 orders.");
    }
}

public class CreateWaveHandler : IRequestHandler<CreateWaveRequest, WaveResult>
{
    private readonly ICommandRepository<PickWave> _waves;
    private readonly ICommandRepository<PickWaveOrder> _links;
    private readonly ICommandRepository<InventoryTransaction> _transactions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;

    public CreateWaveHandler(ICommandRepository<PickWave> waves, ICommandRepository<PickWaveOrder> links, ICommandRepository<InventoryTransaction> transactions, IUnitOfWork unitOfWork, NumberSequenceService numbers)
    {
        _waves = waves; _links = links; _transactions = transactions; _unitOfWork = unitOfWork; _numbers = numbers;
    }

    public async Task<WaveResult> Handle(CreateWaveRequest request, CancellationToken ct)
    {
        var ids = (request.DeliveryOrderIds ?? new List<string>()).Distinct().ToList();

        var openWaveIds = _waves.GetQuery().ApplyIsDeletedFilter().Where(w => w.Status == "Open").Select(w => w.Id).ToList();
        var taken = _links.GetQuery().ApplyIsDeletedFilter().Where(l => openWaveIds.Contains(l.PickWaveId!)).Select(l => l.DeliveryOrderId).ToList();
        if (ids.Any(i => taken.Contains(i))) throw new Exception("One of the orders is already in an open wave.");

        // all orders must ship from the same warehouse, otherwise one walk cannot serve them
        var warehouses = _transactions.GetQuery().ApplyIsDeletedFilter()
            .Where(x => x.ModuleName == nameof(DeliveryOrder) && ids.Contains(x.ModuleId!) && x.WarehouseId != null)
            .Select(x => x.WarehouseId).Distinct().ToList();
        if (warehouses.Count > 1) throw new Exception("These orders ship from different warehouses. Put orders from one warehouse in each wave.");

        var wave = new PickWave
        {
            CreatedById = request.CreatedById, Status = "Open", Number = _numbers.GenerateNumber(nameof(PickWave), "", "WAV"),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };
        await _waves.CreateAsync(wave, ct);
        foreach (var id in ids) await _links.CreateAsync(new PickWaveOrder { CreatedById = request.CreatedById, PickWaveId = wave.Id, DeliveryOrderId = id }, ct);
        await _unitOfWork.SaveAsync(ct);
        return new WaveResult { Id = wave.Id, Number = wave.Number, Orders = ids.Count };
    }
}

// ---------------------------------------------------------------- cancel
public class CancelWaveRequest : IRequest<WaveResult>
{
    public string? WaveId { get; init; }
    public string? UpdatedById { get; init; }
}

public class CancelWaveHandler : IRequestHandler<CancelWaveRequest, WaveResult>
{
    private readonly ICommandRepository<PickWave> _waves;
    private readonly IUnitOfWork _unitOfWork;

    public CancelWaveHandler(ICommandRepository<PickWave> waves, IUnitOfWork unitOfWork)
    {
        _waves = waves; _unitOfWork = unitOfWork;
    }

    public async Task<WaveResult> Handle(CancelWaveRequest request, CancellationToken ct)
    {
        var w = await _waves.GetAsync(request.WaveId ?? string.Empty, ct) ?? throw new Exception("Wave not found.");
        if (w.Status != "Open") throw new Exception($"{w.Number} is {w.Status} and cannot be cancelled.");
        w.Status = "Cancelled";
        w.UpdatedById = request.UpdatedById;
        _waves.Update(w);
        await _unitOfWork.SaveAsync(ct);
        return new WaveResult { Id = w.Id, Number = w.Number };
    }
}

// ---------------------------------------------------------------- confirm the combined pick
public class WavePickInput
{
    public string? ProductId { get; init; }
    public string? LocationId { get; init; }
    public double Quantity { get; init; }
}

public class ConfirmWavePickRequest : IRequest<WaveResult>
{
    public string? WaveId { get; init; }
    public List<WavePickInput>? Picks { get; init; }
    public string? UpdatedById { get; init; }
}

public class ConfirmWavePickValidator : AbstractValidator<ConfirmWavePickRequest>
{
    public ConfirmWavePickValidator()
    {
        RuleFor(x => x.WaveId).NotEmpty();
        RuleFor(x => x.Picks).NotEmpty().WithMessage("Enter a quantity to pick.");
    }
}

/// <summary>
/// Takes what was picked in one combined walk and shares it back over the orders, oldest first, by recording a normal
/// pick for each order. When every order is fully picked the wave is closed.
/// </summary>
public class ConfirmWavePickHandler : IRequestHandler<ConfirmWavePickRequest, WaveResult>
{
    private readonly ICommandRepository<PickWave> _waves;
    private readonly ICommandRepository<PickWaveOrder> _links;
    private readonly ISender _sender;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmWavePickHandler(ICommandRepository<PickWave> waves, ICommandRepository<PickWaveOrder> links, ISender sender, IUnitOfWork unitOfWork)
    {
        _waves = waves; _links = links; _sender = sender; _unitOfWork = unitOfWork;
    }

    public async Task<WaveResult> Handle(ConfirmWavePickRequest request, CancellationToken ct)
    {
        var wave = await _waves.GetAsync(request.WaveId ?? string.Empty, ct) ?? throw new Exception("Wave not found.");
        if (wave.Status != "Open") throw new Exception($"{wave.Number} is {wave.Status}.");

        var orderIds = _links.GetQuery().ApplyIsDeletedFilter().Where(l => l.PickWaveId == wave.Id).Select(l => l.DeliveryOrderId!).ToList();
        var data = await WaveAllocator.Load(_sender, orderIds, ct);

        // the pool of units picked, per product and location, in the order they were entered
        var pool = (request.Picks ?? new List<WavePickInput>()).Where(p => p.Quantity > 0 && p.ProductId != null && p.LocationId != null)
            .Select(p => (p.ProductId!, p.LocationId!, Qty: p.Quantity)).ToList();
        if (pool.Count == 0) throw new Exception("Enter a quantity to pick.");

        var wanted = data.SelectMany(d => d.Lines).GroupBy(l => l.ProductId ?? "").ToDictionary(g => g.Key, g => g.Sum(l => l.Remaining));
        foreach (var g in pool.GroupBy(p => p.Item1))
        {
            if (!wanted.TryGetValue(g.Key, out var max)) throw new Exception("A picked product is not on the orders of this wave.");
            if (g.Sum(p => p.Qty) > max + 1e-9) throw new Exception($"You are picking {g.Sum(p => p.Qty):0.##} of a product but the orders of this wave need only {max:0.##} more.");
        }

        var left = pool.Select(p => (p.Item1, p.Item2, Qty: p.Qty)).ToList();
        var result = new WaveResult { Id = wave.Id, Number = wave.Number };
        foreach (var o in data)
        {
            var picks = new List<PickLineInput>();
            foreach (var line in o.Lines.Where(l => l.Remaining > 1e-9))
            {
                var need = line.Remaining;
                for (var i = 0; i < left.Count && need > 1e-9; i++)
                {
                    if (left[i].Item1 != line.ProductId || left[i].Qty <= 1e-9) continue;
                    var take = Math.Min(need, left[i].Qty);
                    picks.Add(new PickLineInput { ProductId = line.ProductId, LocationId = left[i].Item2, Quantity = take });
                    left[i] = (left[i].Item1, left[i].Item2, left[i].Qty - take);
                    need -= take;
                }
            }
            if (picks.Count == 0) continue;
            try
            {
                var r = await _sender.Send(new ConfirmPickRequest { DeliveryOrderId = o.Order.Id, Picks = picks, UpdatedById = request.UpdatedById }, ct);
                result.Orders++;
                result.Units += r.Units;
            }
            catch (Exception ex)
            {
                throw new Exception($"{o.Order.Number}: {ex.Message}" + (result.Orders > 0 ? $" ({result.Orders} earlier order(s) were already picked; reload the wave to see what is left.)" : ""));
            }
        }

        var after = await WaveAllocator.Load(_sender, orderIds, ct);
        result.Done = after.All(d => d.Lines.All(l => l.Remaining <= 1e-9));
        if (result.Done)
        {
            wave.Status = "Picked";
            wave.UpdatedById = request.UpdatedById;
            _waves.Update(wave);
            await _unitOfWork.SaveAsync(ct);
        }
        return result;
    }
}
