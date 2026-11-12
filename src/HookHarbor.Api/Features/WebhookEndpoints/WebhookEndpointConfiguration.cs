using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookHarbor.Api.Features.WebhookEndpoints;

public sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("webhook_endpoints");

        builder.HasKey(x=> x.Id);

        builder.Property(x=> x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x=> x.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x=> x.InboundKey)
            .HasColumnName("inbound_key")
            .HasMaxLength(48)
            .IsRequired();

        builder.HasIndex(x=> x.InboundKey)
            .IsUnique();

        builder.Property(x=> x.DestinationUrl)
            .HasColumnName("destination_url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.SigningSecret)
            .HasColumnName("signing_secret")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x=> x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}