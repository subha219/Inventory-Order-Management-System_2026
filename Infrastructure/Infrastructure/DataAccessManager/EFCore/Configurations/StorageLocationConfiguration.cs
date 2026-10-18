using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class StorageLocationConfiguration : BaseEntityConfiguration<StorageLocation>
{
    public override void Configure(EntityTypeBuilder<StorageLocation> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.WarehouseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Code).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Zone).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Aisle).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Rack).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Shelf).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Bin).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.LocationType).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Capacity).IsRequired(false);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.Notes).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.WarehouseId);
        builder.HasIndex(e => e.Code);
    }
}

public class LocationStockConfiguration : BaseEntityConfiguration<LocationStock>
{
    public override void Configure(EntityTypeBuilder<LocationStock> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.LocationId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);

        builder.HasIndex(e => e.LocationId);
        builder.HasIndex(e => e.ProductId);
    }
}
