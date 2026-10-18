using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class StockHoldConfiguration : BaseEntityConfiguration<StockHold>
{
    public override void Configure(EntityTypeBuilder<StockHold> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.WarehouseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);
        builder.Property(x => x.OriginalQuantity).IsRequired(false);
        builder.Property(x => x.Reason).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.ReleasedAtUtc).IsRequired(false);
        builder.Property(x => x.ReleasedById).HasMaxLength(450).IsRequired(false);
        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.Status);
    }
}
