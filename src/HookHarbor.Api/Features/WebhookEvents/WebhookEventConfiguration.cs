using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookHarbor.Api.Features.WebhookEvents;

public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.WebhookEndpointId)
            .HasColumnName("webhook_endpoint_id")
            .IsRequired();

        builder.Property(x => x.Body)
            .HasColumnName("body")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(255);

        builder.Property(x => x.Headers)
            .HasColumnName("headers")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ReceivedAt)
            .HasColumnName("received_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(x => x.WebhookEndpoint)
            .WithMany()
            .HasForeignKey(x => x.WebhookEndpointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
        {
            x.WebhookEndpointId,
            x.ReceivedAt
        });
    }
}
