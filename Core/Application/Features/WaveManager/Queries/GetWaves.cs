using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.WaveManager.Queries;

public class WaveOptionDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Customer { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public double Remaining { get; init; }
}

public class WaveSummaryDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Status { get; init; }
    public int Orders { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public string? Notes { get; init; }
}

public class GetWavesResult
{
    public List<WaveOptionDto> Available { get; init; } = new();
    public List<WaveSummaryDto> Waves { get; init; } = new();
    public string? WaveNumber { get; init; }
    public string? WaveStatus { get; init; }
    public List<WaveOrderDto> Orders { get; init; } = new();
    public List<WaveRowDto> Rows { get; init; } = new();
    public List<WaveShortDto> Shortfalls { get; init; } = new();
}

public class GetWavesRequest : IRequest<GetWavesResult>
{
    public string? WaveId { get; init; }
}

public class GetWavesHandler : IRequestHandler<GetWavesRequest, GetWavesResult>
{
    private readonly IQueryContext _context;
    private readonly ISender _sender;

    public GetWavesHandler(IQueryContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<GetWavesResult> Handle(GetWavesRequest request, CancellationToken ct)
    {
        var waves = await _context.PickWave.AsNoTracking().ApplyIsDeletedFilter(false).OrderByDescending(w => w.CreatedAtUtc).Take(60).ToListAsync(ct);
        var waveIds = waves.Select(w => w.Id).ToList();
        var links = await _context.PickWaveOrder.AsNoTracking().ApplyIsDeletedFilter(false).Where(l => waveIds.Contains(l.PickWaveId)).ToListAsync(ct);
        var openWaveIds = waves.Where(w => w.Status == "Open").Select(w => w.Id).ToHashSet();
        var inOpenWave = links.Where(l => openWaveIds.Contains(l.PickWaveId!)).Select(l => l.DeliveryOrderId).ToHashSet();

        var all = await _sender.Send(new PickListManager.Queries.GetPickListRequest(), ct);
        var available = all.Orders.Where(o => o.Needed - o.Picked > 1e-9 && !inOpenWave.Contains(o.Id))
            .Select(o => new WaveOptionDto { Id = o.Id, Number = o.Number, Customer = o.Customer, DeliveryDate = o.DeliveryDate, Remaining = o.Needed - o.Picked })
            .OrderBy(o => o.DeliveryDate).ToList();

        var summaries = waves.Select(w => new WaveSummaryDto
        {
            Id = w.Id, Number = w.Number, Status = w.Status, Orders = links.Count(l => l.PickWaveId == w.Id), CreatedAtUtc = w.CreatedAtUtc, Notes = w.Notes
        }).ToList();

        var result = new GetWavesResult { Available = available, Waves = summaries };
        var wave = waves.FirstOrDefault(w => w.Id == request.WaveId);
        if (wave == null) return result;

        var orderIds = links.Where(l => l.PickWaveId == wave.Id).Select(l => l.DeliveryOrderId!).ToList();
        var data = await WaveAllocator.Load(_sender, orderIds, ct);
        var alloc = wave.Status == "Open" ? WaveAllocator.Allocate(data) : new WaveAllocation();
        return new GetWavesResult
        {
            Available = available, Waves = summaries, WaveNumber = wave.Number, WaveStatus = wave.Status,
            Orders = data.Select(d => new WaveOrderDto { Id = d.Order.Id, Number = d.Order.Number, Customer = d.Order.Customer, DeliveryDate = d.Order.DeliveryDate, Needed = d.Order.Needed, Picked = d.Order.Picked }).ToList(),
            Rows = alloc.Rows, Shortfalls = alloc.Shortfalls
        };
    }
}
