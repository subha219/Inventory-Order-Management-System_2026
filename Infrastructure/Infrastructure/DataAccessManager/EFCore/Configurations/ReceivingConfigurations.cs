using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class QualityInspectionConfiguration : BaseEntityConfiguration<QualityInspection>
{
    public override void Configure(EntityTypeBuilder<QualityInspection> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.GoodsReceiveId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.PassedQuantity).IsRequired(false);
        builder.Property(x => x.FailedQuantity).IsRequired(false);
        builder.Property(x => x.Result).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Reason).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.GoodsReceiveId);
    }
}

public class PutAwayRecordConfiguration : BaseEntityConfiguration<PutAwayRecord>
{
    public override void Configure(EntityTypeBuilder<PutAwayRecord> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.GoodsReceiveId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.LocationId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);

        builder.HasIndex(e => e.GoodsReceiveId);
    }
}
