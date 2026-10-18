using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ProductBomLineConfiguration : BaseEntityConfiguration<ProductBomLine>
{
    public override void Configure(EntityTypeBuilder<ProductBomLine> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.KitProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ComponentProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);
        builder.HasIndex(e => e.KitProductId);
    }
}

public class KitAssemblyConfiguration : BaseEntityConfiguration<KitAssembly>
{
    public override void Configure(EntityTypeBuilder<KitAssembly> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Number).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.KitProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.WarehouseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);
        builder.Property(x => x.ComponentsDocument).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.KitsDocument).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.HasIndex(e => e.KitProductId);
    }
}
