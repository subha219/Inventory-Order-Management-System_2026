using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ReturnInspectionConfiguration : BaseEntityConfiguration<ReturnInspection>
{
    public override void Configure(EntityTypeBuilder<ReturnInspection> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.SalesReturnId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ProductId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ReturnedQuantity).IsRequired(false);
        builder.Property(x => x.GoodQuantity).IsRequired(false);
        builder.Property(x => x.DamagedQuantity).IsRequired(false);
        builder.Property(x => x.ReasonCode).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Note).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.LocationId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.HasIndex(e => e.SalesReturnId);
    }
}
