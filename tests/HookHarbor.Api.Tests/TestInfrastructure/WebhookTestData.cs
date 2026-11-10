using System.Net;
using System.Net.Http.Json;
using System.Text;
using HookHarbor.Api.Features.WebhookEndpoints.Requests;
using HookHarbor.Api.Features.WebhookEndpoints.Responses;
using HookHarbor.Api.Features.WebhookEvents.Responses;

namespace HookHarbor.Api.Tests.TestInfrastructure;

public sealed class WebhookTestData(HttpClient client)
{
    public async Task<WebhookEndpointResponse> CreateEndpointAsync(
        string? destinationUrl = null,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        destinationUrl ??= "https://destination.example.com/webhooks";
        name ??= $"Test endpoint {Guid.NewGuid()}";

        using var response = await client.PostAsJsonAsync(
            "/api/endpoints",
            new CreateWebhookEndpointRequest(
                name,
                destinationUrl),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response
            .Content
            .ReadFromJsonAsync<WebhookEndpointResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The endpoint response body was empty.");
    }

    public async Task<Guid> CreateEventAsync(
        string inboundKey,
        string body,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/in/{inboundKey}");

        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            throw new InvalidOperationException($"Webhook ingestion returned HTTP {(int)response.StatusCode}");
        }

        var accepted = await response
            .Content
            .ReadFromJsonAsync<WebhookEventAcceptedResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The ingestion response body was empty.");

        return accepted.Id;
    }
}
