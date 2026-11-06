namespace HookHarbor.Api.Features.WebhookEvents;

public enum WebhookEventStatus
{
    Pending,
    RetryScheduled,
    Delivered,
    Failed
}