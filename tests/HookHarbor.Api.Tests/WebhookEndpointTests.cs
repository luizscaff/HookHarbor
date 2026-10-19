using HookHarbor.Api.Features.WebhookEndpoints.Requests;
using HookHarbor.Api.Features.WebhookEndpoints.Responses;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace HookHarbor.Api.Tests;

public sealed class WebhookEndpointTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WebhookEndpointTests(HookHarborWebApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            }
        );
    }

    [Fact]
    public async Task Create_WithValidRequest_PersistsEndpoint()
    {
        var request = new CreateWebhookEndpointRequest("Stripe production", "https://example.com/webhooks/stripe");

        var createResponse = await _client.PostAsJsonAsync("/api/endpoints", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<WebhookEndpointResponse>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Stripe production", created.Name);
        Assert.Equal(48, created.InboundKey.Length);
        Assert.NotNull(createResponse.Headers.Location);

        var getResponse = await _client.GetAsync($"/api/endpoints/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrieved = await getResponse.Content.ReadFromJsonAsync<WebhookEndpointResponse>();

        Assert.Equal(created, retrieved);
    }

    [Fact]
    public async Task Create_WithInvalidUrl_ReturnsBadRequest()
    {
        var request = new CreateWebhookEndpointRequest("Invalid endpoint", "not-a-url");

        var createResponse = await _client.PostAsJsonAsync("/api/endpoints", request);

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
    }

    [Fact]
    public async Task Get_WithUnknownId_ReturnsNotFound()
    {
        var getResponse = await _client.GetAsync($"/api/endpoints/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}