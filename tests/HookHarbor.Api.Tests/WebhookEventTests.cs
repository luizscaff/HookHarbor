using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookEndpoints.Requests;
using HookHarbor.Api.Features.WebhookEndpoints.Responses;
using HookHarbor.Api.Features.WebhookEvents;
using HookHarbor.Api.Features.WebhookEvents.Responses;
using HookHarbor.Api.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Validation;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace HookHarbor.Api.Tests;

public sealed class WebhookEventTests(HookHarborWebApplicationFactory factory) : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Receive_WithKnownInboundKey_PersistsEvent()
    {
        var endpointResponse = await _client.PostAsJsonAsync(
            "api/endpoints",
            new CreateWebhookEndpointRequest(
                "Stripe production",
                "https://example.com/webhooks/stripe"
            )
        );

        endpointResponse.EnsureSuccessStatusCode();

        var endpoint = await endpointResponse
            .Content
            .ReadFromJsonAsync<WebhookEndpointResponse>()
            ?? throw new InvalidOperationException("The endpoint response body was empty");

        const string body = """
           {
                "type": "payment.succeeded",
                "customer_id": 123,
                "amount": 4490
           }
        """;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/in/{endpoint.InboundKey}"
        );

        request.Content = new StringContent(
            body,
            Encoding.UTF8,
            "application/json"
        );

        request.Headers.Add(
            "X-Webhook-Source",
            "integration-test"
        );

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var accepted = await response
            .Content
            .ReadFromJsonAsync<WebhookEventAcceptedResponse>()
            ?? throw new InvalidOperationException("The accepted response body was empty");

        Assert.Equal("Pending", accepted.Status);

        await using var scope = factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var storedEvent = await dbContext.WebhookEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accepted.Id) ?? throw new InvalidOperationException("The webhook event was not persisted.");
        
        Assert.Equal(endpoint.Id, storedEvent.WebhookEndpointId);

        Assert.Equal(Encoding.UTF8.GetBytes(body), storedEvent.Body);

        Assert.Equal(WebhookEventStatus.Pending, storedEvent.Status);

        Assert.NotNull(storedEvent.ContentType);
        Assert.StartsWith("application/json", storedEvent.ContentType);

        var storedHeaders = storedEvent.Headers ?? throw new InvalidOperationException("The stored event headers were null");

        using var headers = JsonDocument.Parse(storedEvent.Headers);

        Assert.Equal("integration-test", headers.RootElement.GetProperty("X-Webhook-Source")[0].GetString());
    }

    [Fact]
    public async Task Receive_WithUnknownInboundKey_ReturnsNotFound()
    {
        using var content = new StringContent(
            """{"type":"test"}""",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync(
            "/in/unknown-inbound-key",
            content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
