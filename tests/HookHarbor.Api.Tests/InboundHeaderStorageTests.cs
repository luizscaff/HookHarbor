using HookHarbor.Api.Data;
using HookHarbor.Api.Security;
using HookHarbor.Api.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace HookHarbor.Api.Tests;

public sealed class InboundHeaderStorageTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HookHarborWebApplicationFactory _factory;
    private readonly WebhookTestData _testData;

    public InboundHeaderStorageTests(HookHarborWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _testData = new WebhookTestData(_client);
    }

    [Fact]
    public async Task Receive_StoresProviderHeadersButExcludesManagementApiKey()
    {
        const string stripeSignature = "t=1731000000,v1=test-signature";

        var endpoint = await _testData.CreateEndpointAsync();

        var eventId = await _testData.CreateEventAsync(
            endpoint.InboundKey,
            """{"type": "payment.succeeded"}""",
            new Dictionary<string, string>
            {
                ["Stripe-Signature"] = stripeSignature
            }
        );

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var webhookEvent = await dbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleAsync(x =>  x.Id == eventId);

        var storedHeaders = JsonSerializer
            .Deserialize<Dictionary<string, string[]>> (webhookEvent.Headers) 
            ?? throw new InvalidOperationException("Stored webhook headers were empty.");

        Assert.True(storedHeaders.TryGetValue("Stripe-Signature", out var signatureValues));
        Assert.Equal(stripeSignature, Assert.Single(signatureValues));
        Assert.False(storedHeaders.ContainsKey(ManagementApiKeyMiddleware.HeaderName));
    }
}
