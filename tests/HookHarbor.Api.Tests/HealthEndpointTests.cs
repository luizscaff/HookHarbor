using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using HookHarbor.Api.Tests.TestInfrastructure;

namespace HookHarbor.Api.Tests;

public class HealthEndpointTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(HookHarborWebApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyBody()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }
}
