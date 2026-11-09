namespace HookHarbor.Api.Features.WebhookEvents;

public enum WebhookEventStatus
{
    Pending,
    Processing,
    RetryScheduled,
    Delivered,
    Failed
}