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

public sealed class WebhookEventClaimServiceTests(HookHarborWebApplicationFactory factory) : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HookHarborWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TryClaimAsync_WhenCalledConcurrently_AllowsOnlyOneClaim()
    {
        var webhookEventId = await CreateWebhookEventAsync();

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

    private async Task<Guid> CreateWebhookEventAsync()
    {
        using var endpointResponse = await _client.PostAsJsonAsync(
            "/api/endpoints",
            new CreateWebhookEndpointRequest($"Claim test {Guid.NewGuid()}",
            "https://destination.example.com/webhooks"));

        endpointResponse.EnsureSuccessStatusCode();

        var endpoint = await endpointResponse
            .Content
            .ReadFromJsonAsync<WebhookEndpointResponse>()
            ?? throw new InvalidOperationException("The endpoint response body was empty.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/in/{endpoint.InboundKey}");

        request.Content = new StringContent("""{"type": "claim.test"}""", Encoding.UTF8, "application/json");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var accepted = await response
            .Content
            .ReadFromJsonAsync<WebhookEventAcceptedResponse>()
            ?? throw new InvalidOperationException("The ingestion response body was empty.");

        return accepted.Id;
    }
}
