namespace HookHarbor.Api.Features.WebhookDeliveryAttempts.Configuration;

public sealed class WebhookDeliveryOptions
{
    public const string SectionName = "WebhookDelivery";
    public int MaximumCapturedResponseBodyBytes { get; init; } = 1_048_576;
}
