using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccessManager.EFCore.Configurations;

public class IntegrationApiKeyConfiguration : BaseEntityConfiguration<IntegrationApiKey>
{
    public override void Configure(EntityTypeBuilder<IntegrationApiKey> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.KeyPrefix).HasMaxLength(20).IsRequired(false);
        builder.Property(x => x.KeyHash).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired(false);
        builder.Property(x => x.LastUsedAtUtc).IsRequired(false);
        builder.HasIndex(e => e.KeyHash);
    }
}

public class WebhookEndpointConfiguration : BaseEntityConfiguration<WebhookEndpoint>
{
    public override void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Secret).HasMaxLength(100).IsRequired(false);
        builder.Property(x => x.Events).HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired(false);
        builder.Property(x => x.LastStatus).HasMaxLength(300).IsRequired(false);
        builder.Property(x => x.LastAtUtc).IsRequired(false);
    }
}
