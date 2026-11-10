using System.Net;
using System.Net.Http.Json;
using System.Text;
using HookHarbor.Api.Tests.TestInfrastructure;
using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookEndpoints.Requests;
using HookHarbor.Api.Features.WebhookEndpoints.Responses;
using HookHarbor.Api.Features.WebhookEvents;
using HookHarbor.Api.Features.WebhookEvents.Responses;
using HookHarbor.Api.Features.WebhookEvents.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Tests;

public sealed class WebhookEventClaimServiceTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HookHarborWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly WebhookTestData _testData;

    public WebhookEventClaimServiceTests(HookHarborWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _testData = new WebhookTestData(_client);
    }

    [Fact]
    public async Task TryClaimAsync_WhenCalledConcurrently_AllowsOnlyOneClaim()
    {
        var endpoint = await _testData.CreateEndpointAsync();
        var webhookEventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "test"}""");

        await using var firstScope = _factory.Services.CreateAsyncScope();
        var firstClaimService = firstScope.ServiceProvider.GetRequiredService<WebhookEventClaimService>();

        await using var secondScope = _factory.Services.CreateAsyncScope();
        var secondClaimService = secondScope.ServiceProvider.GetRequiredService<WebhookEventClaimService>();

        var now = DateTimeOffset.UtcNow;

        var results = await Task.WhenAll(
            firstClaimService.TryClaimAsync(webhookEventId, now, CancellationToken.None),
            secondClaimService.TryClaimAsync(webhookEventId, now, CancellationToken.None));

        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);

        await using var verificationScope = _factory.Services.CreateAsyncScope();

        var dbContext = verificationScope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var storedEvent = await dbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleAsync(x => x.Id == webhookEventId);

        Assert.Equal(WebhookEventStatus.Processing, storedEvent.Status);

        Assert.NotNull(storedEvent.ProcessingStartedAt);
    }
}
