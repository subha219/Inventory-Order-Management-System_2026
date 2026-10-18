using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Common.Constants;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class ScheduledReportConfiguration : BaseEntityConfiguration<ScheduledReport>
{
    public override void Configure(EntityTypeBuilder<ScheduledReport> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.ReportType).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Frequency).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.DayOfWeek).IsRequired(false);
        builder.Property(x => x.DayOfMonth).IsRequired(false);
        builder.Property(x => x.SendHour).IsRequired();
        builder.Property(x => x.SendMinute).IsRequired();
        builder.Property(x => x.Recipients).HasMaxLength(DescriptionConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.Enabled).IsRequired();
        builder.Property(x => x.WarehouseId).HasMaxLength(IdConsts.MaxLength).IsRequired(false);
        builder.Property(x => x.SlowDays).IsRequired(false);
        builder.Property(x => x.LastSentUtc).IsRequired(false);
        builder.Property(x => x.LastStatus).HasMaxLength(NameConsts.MaxLength).IsRequired(false);
    }
}
