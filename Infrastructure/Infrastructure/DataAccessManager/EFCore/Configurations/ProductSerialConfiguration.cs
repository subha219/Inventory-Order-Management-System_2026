using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ProductSerialConfiguration : BaseEntityConfiguration<ProductSerial>
{
    public override void Configure(EntityTypeBuilder<ProductSerial> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.SerialNumber).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.WarehouseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.LocationId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.GoodsReceiveId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.GoodsReceiveNumber).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DeliveryOrderId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DeliveryOrderNumber).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.CustomerName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ShippedAtUtc).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);

        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => e.SerialNumber);
        builder.HasIndex(e => e.ProductId);
    }
}
