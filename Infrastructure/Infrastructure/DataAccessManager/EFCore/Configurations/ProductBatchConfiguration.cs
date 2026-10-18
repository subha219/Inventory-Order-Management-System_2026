using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ProductBatchConfiguration : BaseEntityConfiguration<ProductBatch>
{
    public override void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.WarehouseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.BatchNumber).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ManufactureDate).IsRequired(false);
        builder.Property(x => x.ExpiryDate).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.ProductId);
        builder.HasIndex(e => e.ExpiryDate);
    }
}
