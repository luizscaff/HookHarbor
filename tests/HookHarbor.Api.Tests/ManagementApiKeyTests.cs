using HookHarbor.Api.Security;
using HookHarbor.Api.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace HookHarbor.Api.Tests;

public sealed class ManagementApiKeyTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HookHarborWebApplicationFactory _factory;

    public ManagementApiKeyTests(HookHarborWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ManagementRoute_WithoutApiKey_ReturnsUnauthorized()
    {
        using var client = CreateUnauthenticatedClient();

        using var response = await client.GetAsync($"/api/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response
            .Content
            .ReadFromJsonAsync<ProblemDetails>()
            ?? throw new InvalidOperationException("The unauthorized response body was empty.");

        Assert.Equal(401, problem.Status);
        Assert.Equal("Unauthorized", problem.Title);
    }

    [Fact]
    public async Task ManagementRoute_WithInvalidApiKey_ReturnsUnauthorized()
    {
        using var client = CreateUnauthenticatedClient();

        client.DefaultRequestHeaders.Add(ManagementApiKeyMiddleware.HeaderName, "invalid-api-key");

        using var response = await client.GetAsync($"/api/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ManagementRoute_WithValidApiKey_PassesAuthentication()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/events/{Guid.NewGuid()}");

        //event doesn't exist, but reaching the controller and receiving 404 proves that auth succeeded
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_WithoutApiKey_RemainsPublic()
    {
        using var client = CreateUnauthenticatedClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InboundWebhook_WithoutApiKey_RemainsPublic()
    {
        using var authenticatedClient = _factory.CreateClient();

        var testData = new WebhookTestData(authenticatedClient);

        var endpoint = await testData.CreateEndpointAsync();

        using var unauthenticatedClient = CreateUnauthenticatedClient();

        using var content = new StringContent("""{"type": "public.inbound.test"}""", Encoding.UTF8, "application/json");

        using var response = await unauthenticatedClient.PostAsync($"/in/{endpoint.InboundKey}", content);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private HttpClient CreateUnauthenticatedClient()
    {
        var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Remove(ManagementApiKeyMiddleware.HeaderName);

        return client;
    }
}
