using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class PortalLinkConfiguration : BaseEntityConfiguration<PortalLink>
{
    public override void Configure(EntityTypeBuilder<PortalLink> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.EntityType).HasMaxLength(30).IsRequired(false);
        builder.Property(x => x.EntityId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Token).HasMaxLength(64).IsRequired(false);
        builder.Property(x => x.ExpiresAtUtc).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired(false);
        builder.HasIndex(e => e.Token);
        builder.HasIndex(e => e.EntityId);
    }
}

public class PortalResponseConfiguration : BaseEntityConfiguration<PortalResponse>
{
    public override void Configure(EntityTypeBuilder<PortalResponse> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.PortalLinkId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.ExpectedDate).IsRequired(false);
        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired(false);
        builder.HasIndex(e => e.PortalLinkId);
    }
}
