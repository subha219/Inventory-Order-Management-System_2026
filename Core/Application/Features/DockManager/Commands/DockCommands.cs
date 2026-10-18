using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DockManager.Commands;

public class DockResult
{
    public string? Id { get; set; }
    public string? Message { get; set; }
}

// ---------------------------------------------------------------- doors
public class SaveDockDoorRequest : IRequest<DockResult>
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? WarehouseId { get; init; }
    public string? DoorType { get; init; }
    public bool IsActive { get; init; } = true;
    public string? UserId { get; init; }
}

public class SaveDockDoorValidator : AbstractValidator<SaveDockDoorRequest>
{
    public SaveDockDoorValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Choose the warehouse.");
        RuleFor(x => x.DoorType).Must(t => t == "Inbound" || t == "Outbound" || t == "Both").WithMessage("Door type must be Inbound, Outbound or Both.");
    }
}

public class SaveDockDoorHandler : IRequestHandler<SaveDockDoorRequest, DockResult>
{
    private readonly ICommandRepository<DockDoor> _doors;
    private readonly IUnitOfWork _unitOfWork;

    public SaveDockDoorHandler(ICommandRepository<DockDoor> doors, IUnitOfWork unitOfWork)
    {
        _doors = doors; _unitOfWork = unitOfWork;
    }

    public async Task<DockResult> Handle(SaveDockDoorRequest r, CancellationToken ct)
    {
        DockDoor? d = string.IsNullOrEmpty(r.Id) ? null : await _doors.GetAsync(r.Id, ct);
        if (d == null)
        {
            d = new DockDoor { CreatedById = r.UserId };
            _doors.Create(d);
        }
        else d.UpdatedById = r.UserId;
        d.Name = r.Name?.Trim(); d.WarehouseId = r.WarehouseId; d.DoorType = r.DoorType; d.IsActive = r.IsActive;
        if (d.UpdatedById != null) _doors.Update(d);
        await _unitOfWork.SaveAsync(ct);
        return new DockResult { Id = d.Id, Message = "Door saved." };
    }
}

public class DeleteDockDoorRequest : IRequest<DockResult>
{
    public string? Id { get; init; }
    public string? UserId { get; init; }
}

public class DeleteDockDoorHandler : IRequestHandler<DeleteDockDoorRequest, DockResult>
{
    private readonly ICommandRepository<DockDoor> _doors;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDockDoorHandler(ICommandRepository<DockDoor> doors, IQueryContext context, IUnitOfWork unitOfWork)
    {
        _doors = doors; _context = context; _unitOfWork = unitOfWork;
    }

    public async Task<DockResult> Handle(DeleteDockDoorRequest r, CancellationToken ct)
    {
        var d = await _doors.GetAsync(r.Id ?? string.Empty, ct) ?? throw new Exception("Door not found.");
        var id = d.Id;
        var open = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false)
            .AnyAsync(a => a.DockDoorId == id && (a.Status == "Scheduled" || a.Status == "CheckedIn"), ct);
        if (open) throw new Exception("This door still has open appointments. Cancel them first, or mark the door inactive.");
        d.UpdatedById = r.UserId;
        _doors.Delete(d);
        await _unitOfWork.SaveAsync(ct);
        return new DockResult { Id = id, Message = "Door removed." };
    }
}

// ---------------------------------------------------------------- appointments
public class SaveDockAppointmentRequest : IRequest<DockResult>
{
    public string? Id { get; init; }
    public string? DockDoorId { get; init; }
    public string? Direction { get; init; }
    public DateTime StartAtUtc { get; init; }
    public int DurationMinutes { get; init; } = 60;
    public string? Carrier { get; init; }
    public string? Reference { get; init; }
    public string? PartyName { get; init; }
    public string? Notes { get; init; }
    public string? UserId { get; init; }
}

public class SaveDockAppointmentValidator : AbstractValidator<SaveDockAppointmentRequest>
{
    public SaveDockAppointmentValidator()
    {
        RuleFor(x => x.DockDoorId).NotEmpty().WithMessage("Choose the dock door.");
        RuleFor(x => x.Direction).Must(t => t == "Inbound" || t == "Outbound").WithMessage("Direction must be Inbound or Outbound.");
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 720).WithMessage("Duration must be between 5 minutes and 12 hours.");
        RuleFor(x => x.StartAtUtc).NotEmpty();
    }
}

public class SaveDockAppointmentHandler : IRequestHandler<SaveDockAppointmentRequest, DockResult>
{
    private readonly ICommandRepository<DockAppointment> _appts;
    private readonly ICommandRepository<DockDoor> _doors;
    private readonly IQueryContext _context;
    private readonly NumberSequenceService _numbers;
    private readonly IUnitOfWork _unitOfWork;

    public SaveDockAppointmentHandler(ICommandRepository<DockAppointment> appts, ICommandRepository<DockDoor> doors, IQueryContext context, NumberSequenceService numbers, IUnitOfWork unitOfWork)
    {
        _appts = appts; _doors = doors; _context = context; _numbers = numbers; _unitOfWork = unitOfWork;
    }

    public async Task<DockResult> Handle(SaveDockAppointmentRequest r, CancellationToken ct)
    {
        var door = await _doors.GetAsync(r.DockDoorId ?? string.Empty, ct) ?? throw new Exception("Dock door not found.");
        if (door.IsActive == false) throw new Exception("This door is inactive.");
        if (door.DoorType == "Inbound" && r.Direction == "Outbound") throw new Exception("This door only handles inbound trucks.");
        if (door.DoorType == "Outbound" && r.Direction == "Inbound") throw new Exception("This door only handles outbound trucks.");

        var start = DateTime.SpecifyKind(r.StartAtUtc, DateTimeKind.Utc);
        var end = start.AddMinutes(r.DurationMinutes);

        // overlap check against live appointments on the same door
        var doorId = door.Id;
        var day = await _context.DockAppointment.AsNoTracking().ApplyIsDeletedFilter(false)
            .Where(a => a.DockDoorId == doorId && (a.Status == "Scheduled" || a.Status == "CheckedIn")
                        && a.StartAtUtc >= start.AddHours(-13) && a.StartAtUtc < end).ToListAsync(ct);
        var clash = day.FirstOrDefault(a => a.Id != r.Id && a.StartAtUtc!.Value < end && a.StartAtUtc!.Value.AddMinutes(a.DurationMinutes ?? 60) > start);
        if (clash != null)
            throw new Exception($"{door.Name} is already booked at that time ({clash.Number}, {clash.Carrier}). Pick another time or door.");

        DockAppointment? a = string.IsNullOrEmpty(r.Id) ? null : await _appts.GetAsync(r.Id, ct);
        if (a == null)
        {
            a = new DockAppointment { Number = _numbers.GenerateNumber(nameof(DockAppointment), "", "DCK"), Status = "Scheduled", CreatedById = r.UserId };
            _appts.Create(a);
        }
        else
        {
            if (a.Status != "Scheduled") throw new Exception("Only a scheduled appointment can be changed.");
            a.UpdatedById = r.UserId;
        }
        a.DockDoorId = door.Id; a.Direction = r.Direction; a.StartAtUtc = start; a.DurationMinutes = r.DurationMinutes;
        a.Carrier = r.Carrier?.Trim(); a.Reference = r.Reference?.Trim(); a.PartyName = r.PartyName?.Trim(); a.Notes = r.Notes?.Trim();
        if (a.UpdatedById != null) _appts.Update(a);
        await _unitOfWork.SaveAsync(ct);
        return new DockResult { Id = a.Id, Message = "Appointment " + a.Number + " saved." };
    }
}

public class SetDockAppointmentStatusRequest : IRequest<DockResult>
{
    public string? Id { get; init; }
    /// <summary>CheckIn, Complete, Cancel or NoShow.</summary>
    public string? Action { get; init; }
    public string? UserId { get; init; }
}

public class SetDockAppointmentStatusHandler : IRequestHandler<SetDockAppointmentStatusRequest, DockResult>
{
    private readonly ICommandRepository<DockAppointment> _appts;
    private readonly IUnitOfWork _unitOfWork;

    public SetDockAppointmentStatusHandler(ICommandRepository<DockAppointment> appts, IUnitOfWork unitOfWork)
    {
        _appts = appts; _unitOfWork = unitOfWork;
    }

    public async Task<DockResult> Handle(SetDockAppointmentStatusRequest r, CancellationToken ct)
    {
        var a = await _appts.GetAsync(r.Id ?? string.Empty, ct) ?? throw new Exception("Appointment not found.");
        switch (r.Action)
        {
            case "CheckIn":
                if (a.Status != "Scheduled") throw new Exception("Only a scheduled truck can check in.");
                a.Status = "CheckedIn"; a.CheckedInAtUtc = DateTime.UtcNow; break;
            case "Complete":
                if (a.Status != "CheckedIn") throw new Exception("The truck must check in before it can be completed.");
                a.Status = "Completed"; break;
            case "Cancel":
                if (a.Status != "Scheduled" && a.Status != "CheckedIn") throw new Exception("This appointment is already closed.");
                a.Status = "Cancelled"; break;
            case "NoShow":
                if (a.Status != "Scheduled") throw new Exception("Only a scheduled appointment can be a no-show.");
                a.Status = "NoShow"; break;
            default: throw new Exception("Unknown action.");
        }
        a.UpdatedById = r.UserId;
        _appts.Update(a);
        await _unitOfWork.SaveAsync(ct);
        return new DockResult { Id = a.Id, Message = a.Number + " is now " + a.Status + "." };
    }
}
