namespace HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;

public static class WebhookSignatureHeaders
{
    public const string EventId = "X-HookHarbor-Event-Id";
    public const string DeliveryId = "X-HookHarbor-Delivery-Id";
    public const string Timestamp = "X-HookHarbor-Timestamp";
    public const string Signature = "X-HookHarbor-Signature";
}
