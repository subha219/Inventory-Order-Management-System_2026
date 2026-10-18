using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class LandedCostConfiguration : BaseEntityConfiguration<LandedCost>
{
    public override void Configure(EntityTypeBuilder<LandedCost> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.GoodsReceiveId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.Amount).IsRequired(false);
        builder.Property(x => x.Basis).HasMaxLength(20).IsRequired(false);
        builder.HasIndex(e => e.GoodsReceiveId);
    }
}
