namespace HookHarbor.Api.Features.WebhookEvents.Responses;

public sealed record WebhookEventSummaryResponse(
    Guid Id,
    Guid WebhookEndpointId,
    string Status,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? NextAttemptAt,
    int AttemptCount);
