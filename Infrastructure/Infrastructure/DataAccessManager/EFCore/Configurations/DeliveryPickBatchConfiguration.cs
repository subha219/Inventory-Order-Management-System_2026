using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class DeliveryPickBatchConfiguration : BaseEntityConfiguration<DeliveryPickBatch>
{
    public override void Configure(EntityTypeBuilder<DeliveryPickBatch> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.DeliveryOrderId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.BatchId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);

        builder.HasIndex(e => e.DeliveryOrderId);
        builder.HasIndex(e => e.BatchId);
    }
}
