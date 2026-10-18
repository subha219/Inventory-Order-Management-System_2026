using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ApprovalRequestConfiguration : BaseEntityConfiguration<ApprovalRequest>
{
    public override void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.EntityType).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EntityId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.EntityNumber).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Units).IsRequired(false);
        builder.Property(x => x.Status).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RequestedById).HasMaxLength(UserIdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DecidedById).HasMaxLength(UserIdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DecidedAtUtc).IsRequired(false);
        builder.Property(x => x.DecisionNote).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.EntityId);
    }
}
