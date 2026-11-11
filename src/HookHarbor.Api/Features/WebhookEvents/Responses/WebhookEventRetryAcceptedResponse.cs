namespace HookHarbor.Api.Features.WebhookEvents.Responses;

public sealed record WebhookEventRetryAcceptedResponse(
    Guid Id,
    string Status,
    DateTimeOffset? NextAttemptAt
);
