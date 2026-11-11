using HookHarbor.Api.Tests.TestInfrastructure;
using System.Net;
using System.Text;

namespace HookHarbor.Api.Tests;

public sealed class InboundRateLimitTests : IClassFixture<LowRateLimitWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly WebhookTestData _testData;

    public InboundRateLimitTests(LowRateLimitWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _testData = new WebhookTestData(_client);
    }

    [Fact]
    public async Task Receive_WhenLimitIsExceeded_ReturnsTooManyRequests()
    {
        var endpoint = await _testData.CreateEndpointAsync();

        for (var requestNumber = 1; requestNumber <= 3; requestNumber++)
        {
            await _testData.CreateEventAsync(endpoint.InboundKey, $$"""{"requestNumber": {{requestNumber}}}""");
        }

        using var content = new StringContent("""{"requestNumber": 4}""", Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync($"/in/{endpoint.InboundKey}", content);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
