namespace HookHarbor.Api.Features.WebhookEvents.Responses;

public sealed record WebhookEventAcceptedResponse(Guid Id, string Status, DateTimeOffset ReceivedAt)
{
    public static WebhookEventAcceptedResponse FromEntity(WebhookEvent webhookEvent)
    {
        return new WebhookEventAcceptedResponse(webhookEvent.Id, webhookEvent.Status.ToString(), webhookEvent.ReceivedAt);
    }
}
