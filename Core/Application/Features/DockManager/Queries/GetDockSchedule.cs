using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DockManager.Queries;

public class DockDoorDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string? DoorType { get; init; }
    public bool IsActive { get; init; }
}

public class DockAppointmentDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? DockDoorId { get; init; }
    public string? DoorName { get; init; }
    public string? Direction { get; init; }
    public DateTime StartAtUtc { get; init; }
    public int DurationMinutes { get; init; }
    public string? Carrier { get; init; }
    public string? Reference { get; init; }
    public string? PartyName { get; init; }
    public string? Status { get; init; }
    public string? Notes { get; init; }
    public DateTime? CheckedInAtUtc { get; init; }
    public bool Late { get; init; }
}

public class DockWarehouseDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class GetDockScheduleResult
{
    public List<DockDoorDto> Doors { get; init; } = new();
    public List<DockAppointmentDto> Appointments { get; init; } = new();
    public List<DockWarehouseDto> Warehouses { get; init; } = new();
}

public class GetDockScheduleRequest : IRequest<GetDockScheduleResult>
{
    /// <summary>Start of the range (UTC). Defaults to today.</summary>
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

public class GetDockScheduleHandler : IRequestHandler<GetDockScheduleRequest, GetDockScheduleResult>
{
    private readonly IQueryContext _context;

    public GetDockScheduleHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetDockScheduleResult> Handle(GetDockScheduleRequest request, CancellationToken ct)
    {
        var from = request.FromUtc ?? DateTime.UtcNow.Date;
        var to = request.ToUtc ?? from.AddDays(1);

        var warehouses = await _context.Warehouse.AsNoTracking().ApplyIsDeletedFilter(false).Where(w => w.SystemWarehouse == false)
            .OrderBy(w => w.Name).Select(w => new DockWarehouseDto { Id = w.Id, Name = w.Name }).ToListAsync(ct);
        var whNames = warehouses.ToDictionary(w => w.Id ?? "", w => w.Name);

        var doorRows = await _context.DockDoor.AsNoTracking().ApplyIsDeletedFilter(false).OrderBy(d => d.Name).ToListAsync(ct);
        var doors = doorRows.Select(d => new DockDoorDto
        {
            Id = d.Id, Name = d.Name, WarehouseId = d.WarehouseId, DoorType = d.DoorType ?? "Both", IsActive = d.IsActive != false,
            WarehouseName = whNames.TryGetValue(d.WarehouseId ?? "", out var wn) ? wn : ""
        }).ToList();
        var doorNames = doors.ToDictionary(d => d.Id ?? "", d => d.Name);

        var rows = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(a => a.StartAtUtc >= from && a.StartAtUtc < to).OrderBy(a => a.StartAtUtc).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var appts = rows.Select(a => new DockAppointmentDto
        {
            Id = a.Id, Number = a.Number, DockDoorId = a.DockDoorId, DoorName = doorNames.TryGetValue(a.DockDoorId ?? "", out var dn) ? dn : "",
            Direction = a.Direction, StartAtUtc = DateTime.SpecifyKind(a.StartAtUtc ?? now, DateTimeKind.Utc), DurationMinutes = a.DurationMinutes ?? 60,
            Carrier = a.Carrier, Reference = a.Reference, PartyName = a.PartyName, Status = a.Status, Notes = a.Notes,
            CheckedInAtUtc = a.CheckedInAtUtc,
            Late = a.Status == "Scheduled" && (a.StartAtUtc ?? now).AddMinutes(15) < now
        }).ToList();

        return new GetDockScheduleResult { Doors = doors, Appointments = appts, Warehouses = warehouses };
    }
}
