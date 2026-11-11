namespace HookHarbor.Api.Features.WebhookEvents.Responses;

public sealed record WebhookEventDetailResponse(
    Guid Id,
    Guid WebhookEndpointId,
    string Status,
    DateTimeOffset ReceivedAt,
    string? ContentType,
    Dictionary<string, string[]> Headers,
    string Body,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? ProcessingStartedAt,
    IReadOnlyList<WebhookDeliveryAttemptResponse> Attempts);
