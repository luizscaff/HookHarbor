namespace HookHarbor.Api.Features.WebhookEndpoints.Responses;

public sealed record WebhookEndpointResponse(
    Guid Id,
    string Name,
    string InboundKey,
    string DestinationUrl,
    string SigningSecret,
    DateTimeOffset CreatedAt)
{
    public static WebhookEndpointResponse FromEntity(WebhookEndpoint endpoint)
    {
        return new WebhookEndpointResponse(
            endpoint.Id,
            endpoint.Name,
            endpoint.InboundKey,
            endpoint.DestinationUrl,
            endpoint.SigningSecret,
            endpoint.CreatedAt
        );
    }
}