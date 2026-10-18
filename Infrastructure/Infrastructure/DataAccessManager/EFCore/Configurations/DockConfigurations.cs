using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class DockDoorConfiguration : BaseEntityConfiguration<DockDoor>
{
    public override void Configure(EntityTypeBuilder<DockDoor> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.WarehouseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DoorType).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired(false);
    }
}

public class DockAppointmentConfiguration : BaseEntityConfiguration<DockAppointment>
{
    public override void Configure(EntityTypeBuilder<DockAppointment> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Number).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.DockDoorId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Direction).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.StartAtUtc).IsRequired(false);
        builder.Property(x => x.DurationMinutes).IsRequired(false);
        builder.Property(x => x.Carrier).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Reference).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.PartyName).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.CheckedInAtUtc).IsRequired(false);
        builder.HasIndex(e => e.DockDoorId);
        builder.HasIndex(e => e.StartAtUtc);
    }
}
