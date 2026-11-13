using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HookHarbor.Api.Features.WebhookDeliveryAttempts;

public sealed class WebhookDeliveryAttemptConfiguration : IEntityTypeConfiguration<WebhookDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryAttempt> builder)
    {
        builder.ToTable("webhook_delivery_attempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.WebhookEventId)
            .HasColumnName("webhook_event_id")
            .IsRequired();

        builder.Property(x => x.AttemptNumber)
            .HasColumnName("attempt_number")
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ResponseStatusCode)
            .HasColumnName("response_status_code");

        builder.Property(x => x.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("bytea");

        builder.Property(x => x.ResponseBodyTruncated)
            .HasColumnName("response_body_truncated")
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(4_000);

        builder.Property(x => x.Succeeded)
            .HasColumnName("succeeded")
            .IsRequired();

        builder.Property(x => x.DestinationUrl)
            .HasColumnName("destination_url")
            .HasMaxLength(2_048)
            .IsRequired();

        builder.HasOne(x => x.WebhookEvent)
            .WithMany()
            .HasForeignKey(x => x.WebhookEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
            {
                x.WebhookEventId,
                x.AttemptNumber
            })
            .IsUnique();
    }
}
