using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class PermissionRuleConfiguration : BaseEntityConfiguration<PermissionRule>
{
    public override void Configure(EntityTypeBuilder<PermissionRule> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.RoleName).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.ActionKey).HasMaxLength(20).IsRequired(false);
        builder.HasIndex(e => e.RoleName);
    }
}
