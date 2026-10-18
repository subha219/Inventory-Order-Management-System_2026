using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class AuditLogConfiguration : BaseEntityConfiguration<AuditLog>
{
    public override void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.UserId).HasMaxLength(UserIdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Action).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EntityName).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EntityId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EntityLabel).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Changes).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => e.EntityName);
    }
}
