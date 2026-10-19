namespace HookHarbor.Api.Features.WebhookEndpoints.Responses;

public sealed record WebhookEndpointResponse(
    Guid Id,
    string Name,
    string InboundKey,
    string DestinationUrl,
    DateTimeOffset CreatedAt)
{
    public static WebhookEndpointResponse FromEntity(WebhookEndpoint endpoint)
    {
        return new WebhookEndpointResponse(
            endpoint.Id,
            endpoint.Name,
            endpoint.InboundKey,
            endpoint.DestinationUrl,
            endpoint.CreatedAt
        );
    }
}