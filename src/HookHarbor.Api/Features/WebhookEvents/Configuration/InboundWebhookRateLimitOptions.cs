namespace HookHarbor.Api.Features.WebhookEvents.Configuration;

public sealed class InboundWebhookRateLimitOptions
{
    public const string SectionName = "RateLimits:InboundWebhooks";
    public int PermitLimit { get; init; }
    public int WindowSeconds { get; init; }
}
