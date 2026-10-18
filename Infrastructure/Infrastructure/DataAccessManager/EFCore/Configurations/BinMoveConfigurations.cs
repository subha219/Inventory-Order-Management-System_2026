using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class LocationMoveConfiguration : BaseEntityConfiguration<LocationMove>
{
    public override void Configure(EntityTypeBuilder<LocationMove> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.FromLocationId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ToLocationId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);
        builder.Property(x => x.MoveType).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Reason).HasMaxLength(255).IsRequired(false);
        builder.HasIndex(e => e.ProductId);
    }
}

public class ReplenishmentRuleConfiguration : BaseEntityConfiguration<ReplenishmentRule>
{
    public override void Configure(EntityTypeBuilder<ReplenishmentRule> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.LocationId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.MinQuantity).IsRequired(false);
        builder.Property(x => x.MaxQuantity).IsRequired(false);
        builder.HasIndex(e => e.LocationId);
    }
}
