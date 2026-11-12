using Docker.DotNet.Models;
using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;
using HookHarbor.Api.Features.WebhookEndpoints.Requests;
using HookHarbor.Api.Features.WebhookEndpoints.Responses;
using HookHarbor.Api.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Net;
using System.Net.Http.Json;

namespace HookHarbor.Api.Tests;

public sealed class WebhookEndpointUpdateTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HookHarborWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly WebhookTestData _testData;

    public WebhookEndpointUpdateTests(HookHarborWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _testData = new WebhookTestData(_client);
    }

    [Fact]
    public async Task Update_WithValidRequest_UpdatesEditableProperties()
    {
        var endpoint = await _testData.CreateEndpointAsync(destinationUrl: "https://old.example.com/webhooks", name: "Old endpoint name");

        var request = new UpdateWebhookEndpointRequest("Updated endpoint name", "https://new.example.com/webhooks");

        using var response = await _client.PutAsJsonAsync($"/api/endpoints/{endpoint.Id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response
            .Content
            .ReadFromJsonAsync<WebhookEndpointResponse>()
            ?? throw new InvalidOperationException("The endpoint response body was empty.");

        Assert.Equal(endpoint.Id, updated.Id);
        Assert.Equal(endpoint.InboundKey, updated.InboundKey);
        Assert.Equal(endpoint.CreatedAt, updated.CreatedAt);
        Assert.Equal("Updated endpoint name", updated.Name);
        Assert.Equal("https://new.example.com/webhooks", updated.DestinationUrl);
    }

    [Fact]
    public async Task Update_WithUnknownEndpoint_ReturnsNotFound()
    {
        var request = new UpdateWebhookEndpointRequest("Updated endpoint", "https://new.example.com/webhooks");

        using var response = await _client.PutAsJsonAsync($"/api/endpoints/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidDestination_ReturnsBadRequest()
    {
        var endpoint = await _testData.CreateEndpointAsync();

        var request = new UpdateWebhookEndpointRequest("Invalid endpoint", "not-a-valid-url");

        using var response = await _client.PutAsJsonAsync($"/api/endpoints/{endpoint.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeliverAsync_AfterEndpointUpdate_UsesNewDestination()
    {
        const string originalDestination = "https://old.example.com/webhooks";
        const string updatedDestination = "https://new.example.com/webhooks";

        var endpoint = await _testData.CreateEndpointAsync(originalDestination);
        var eventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "destination updated"}""");

        var updateRequest = new UpdateWebhookEndpointRequest(endpoint.Name, updatedDestination);

        using var updatedResponse = await _client.PutAsJsonAsync($"/api/endpoints/{endpoint.Id}", updateRequest);

        updatedResponse.EnsureSuccessStatusCode();

        Uri? deliveredUri = null;

        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                deliveredUri = request.RequestUri;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

        using var deliveryHttpClient = new HttpClient(handler);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

            var deliveryService = new WebhookDeliveryService(dbContext, deliveryHttpClient, new WebhookSignatureService());

            await deliveryService.DeliverAsync(eventId, CancellationToken.None);
        }

        Assert.Equal(updatedDestination, deliveredUri?.ToString());

        await using var verificationScope = _factory.Services.CreateAsyncScope();

        var verificationDbContext = verificationScope
            .ServiceProvider
            .GetRequiredService<HookHarborDbContext>();

        var attempt = await verificationDbContext
            .WebhookDeliveryAttempts
            .AsNoTracking()
            .SingleAsync(x => x.WebhookEventId == eventId);

        Assert.Equal(updatedDestination, attempt.DestinationUrl);
    }
}
