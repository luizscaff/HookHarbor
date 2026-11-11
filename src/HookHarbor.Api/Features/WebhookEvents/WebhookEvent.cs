using HookHarbor.Api.Features.WebhookEndpoints;
using HookHarbor.Api.Common;

namespace HookHarbor.Api.Features.WebhookEvents;

public class WebhookEvent
{
    public Guid Id { get; private set; }
    public Guid WebhookEndpointId { get; private set; }
    public byte[] Body { get; private set; } = [];
    public string? ContentType { get; private set; }
    public string Headers { get; private set; } = "{}";
    public WebhookEventStatus Status { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public WebhookEndpoint WebhookEndpoint { get; private set; } = null!;
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }

    private WebhookEvent()
    {

    }

    public WebhookEvent(
        Guid webhookEndpointId,
        byte[] body,
        string? contentType,
        string headers,
        DateTimeOffset receivedAt)
    {
        if (webhookEndpointId == Guid.Empty)
        {
            throw new ArgumentException("Webhook endpoint ID is required.", nameof(webhookEndpointId));
        }

        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(headers);

        Id = Guid.NewGuid();
        WebhookEndpointId = webhookEndpointId;
        Body = body;
        ContentType = contentType;
        Headers = headers;
        Status = WebhookEventStatus.Pending;
        ReceivedAt = DateTimePrecision.TruncateToMicroseconds(receivedAt);
    }

    public void MarkDelivered()
    {
        if (Status == WebhookEventStatus.Delivered)
        {
            return;
        }

        Status = WebhookEventStatus.Delivered;
        NextAttemptAt = null;
        ProcessingStartedAt = null;
    }

    public void MarkFailed()
    {
        if (Status == WebhookEventStatus.Delivered)
        {
            throw new InvalidOperationException("A delivered webhook event cannot be marked as failed.");
        }

        Status = WebhookEventStatus.Failed;
        NextAttemptAt = null;
        ProcessingStartedAt = null;
    }

    public void ScheduleRetry(DateTimeOffset nextAttemptAt)
    {
        if (Status == WebhookEventStatus.Delivered)
        {
            throw new InvalidOperationException("A delivered webhook event cannot be retried");
        }

        Status = WebhookEventStatus.RetryScheduled;
        NextAttemptAt = DateTimePrecision.TruncateToMicroseconds(nextAttemptAt);
        ProcessingStartedAt = null;
    }

    public void ScheduleManualRetry(DateTimeOffset requestedAt)
    {
        if (Status != WebhookEventStatus.Failed)
        {
            throw new InvalidOperationException("Only failed webhook events can be manually retried.");
        }

        Status = WebhookEventStatus.RetryScheduled;
        NextAttemptAt = DateTimePrecision.TruncateToMicroseconds(requestedAt);
        ProcessingStartedAt = null;
    }
}
