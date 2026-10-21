using HookHarbor.Api.Features.WebhookEndpoints;
using HookHarbor.Api.Common;

namespace HookHarbor.Api.Features.WebhookEvents;

public class WebhookEvent
{
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

    public Guid Id { get; private set; }

    public Guid WebhookEndpointId { get; private set; }

    public byte[] Body { get; private set; } = [];

    public string? ContentType { get; private set; }

    public string? Headers { get; private set; } = "{}";

    public WebhookEventStatus Status { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public WebhookEndpoint WebhookEndpoint { get; private set; } = null!;

}
