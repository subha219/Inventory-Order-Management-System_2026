using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class WarehouseTaskConfiguration : BaseEntityConfiguration<WarehouseTask>
{
    public override void Configure(EntityTypeBuilder<WarehouseTask> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Number).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.TaskType).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Reference).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.WarehouseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Priority).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.AssignedToId).HasMaxLength(450).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.DueAtUtc).IsRequired(false);
        builder.Property(x => x.StartedAtUtc).IsRequired(false);
        builder.Property(x => x.CompletedAtUtc).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.AssignedToId);
    }
}
