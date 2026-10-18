using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class PickWaveConfiguration : BaseEntityConfiguration<PickWave>
{
    public override void Configure(EntityTypeBuilder<PickWave> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Number).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
    }
}

public class PickWaveOrderConfiguration : BaseEntityConfiguration<PickWaveOrder>
{
    public override void Configure(EntityTypeBuilder<PickWaveOrder> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.PickWaveId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DeliveryOrderId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.HasIndex(e => e.PickWaveId);
        builder.HasIndex(e => e.DeliveryOrderId);
    }
}
