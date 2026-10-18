using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookHarbor.Api.Features.WebhookEndpoints;

public sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("webhook_endpoints");
        builder.HasKey(endpoint => endpoint.Id);
        builder.Property(endpoint => endpoint.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(endpoint => endpoint.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(endpoint => endpoint.InboundKey).HasColumnName("inbound_key").HasMaxLength(48).IsRequired();
        builder.HasIndex(endpoint => endpoint.InboundKey).IsUnique();
        builder.Property(endpoint => endpoint.DestinationUrl).HasColumnName("destination_url").HasMaxLength(2048).IsRequired();
        builder.Property(endpoint => endpoint.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}