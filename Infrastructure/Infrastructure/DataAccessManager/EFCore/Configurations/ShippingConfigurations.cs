using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ShipmentConfiguration : BaseEntityConfiguration<Shipment>
{
    public override void Configure(EntityTypeBuilder<Shipment> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Number).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.DeliveryOrderId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.Carrier).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.TrackingNumber).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.ShippedAtUtc).IsRequired(false);
        builder.Property(x => x.ShippedById).HasMaxLength(450).IsRequired(false);
        builder.Property(x => x.PublicToken).HasMaxLength(64).IsRequired(false);
        builder.HasIndex(e => e.DeliveryOrderId);
    }
}

public class ShipmentPackageConfiguration : BaseEntityConfiguration<ShipmentPackage>
{
    public override void Configure(EntityTypeBuilder<ShipmentPackage> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.ShipmentId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.PackageNumber).IsRequired(false);
        builder.Property(x => x.PackageType).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.WeightKg).IsRequired(false);
        builder.Property(x => x.LengthCm).IsRequired(false);
        builder.Property(x => x.WidthCm).IsRequired(false);
        builder.Property(x => x.HeightCm).IsRequired(false);
        builder.HasIndex(e => e.ShipmentId);
    }
}

public class ShipmentPackageItemConfiguration : BaseEntityConfiguration<ShipmentPackageItem>
{
    public override void Configure(EntityTypeBuilder<ShipmentPackageItem> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.ShipmentId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ShipmentPackageId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Quantity).IsRequired(false);
        builder.HasIndex(e => e.ShipmentId);
    }
}
