using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class AutomationRuleConfiguration : BaseEntityConfiguration<AutomationRule>
{
    public override void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.RuleType).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Name).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Enabled).IsRequired();
        builder.Property(x => x.Threshold).IsRequired(false);
        builder.Property(x => x.NotifyInApp).IsRequired();
        builder.Property(x => x.EmailRecipients).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.LastCheckedUtc).IsRequired(false);
        builder.Property(x => x.LastCount).IsRequired(false);
        builder.Property(x => x.LastEmailDay).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
    }
}

public class AppNotificationConfiguration : BaseEntityConfiguration<AppNotification>
{
    public override void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.RuleId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.RuleType).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Severity).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Title).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Message).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Link).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ItemCount).IsRequired(false);
        builder.Property(x => x.DayKey).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ReadByIds).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);

        builder.HasIndex(e => e.RuleId);
        builder.HasIndex(e => e.DayKey);
    }
}
