namespace HookHarbor.Api.Features.WebhookEvents.Responses;

public sealed record WebhookDeliveryAttemptResponse(
    Guid Id,
    int AttemptNumber,
    string DestinationUrl,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? ResponseStatusCode,
    string? ResponseBody,
    string? ErrorMessage,
    bool Succeeded);
