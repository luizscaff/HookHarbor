using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;
using HookHarbor.Api.Features.WebhookEvents;
using HookHarbor.Api.Features.WebhookEvents.Responses;
using HookHarbor.Api.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace HookHarbor.Api.Tests;

public sealed class WebhookEventCommandTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HookHarborWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly WebhookTestData _testData;

    public WebhookEventCommandTests(HookHarborWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _testData = new WebhookTestData(_client);
    }

    [Fact]
    public async Task Retry_WithFailedEvent_SchedulesManualRetry()
    {
        var endpoint = await _testData.CreateEndpointAsync();

        var eventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "payment.failed"}""");

        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)));

        using var deliveryHttpClient = new HttpClient(handler);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

            var deliveryService = new WebhookDeliveryService(dbContext, deliveryHttpClient, new WebhookSignatureService());

            await deliveryService.DeliverAsync(eventId, CancellationToken.None);
        }

        using var response = await _client.PostAsync($"/api/events/{eventId}/retry", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var result = await response
            .Content
            .ReadFromJsonAsync<WebhookEventRetryAcceptedResponse>()
            ?? throw new InvalidOperationException("The retry response body was empty.");

        Assert.Equal(eventId, result.Id);
        Assert.Equal("RetryScheduled", result.Status);
        Assert.NotNull(result.NextAttemptAt);

        await using var verificationScope = _factory.Services.CreateAsyncScope();

        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var storedEvent = await verificationDbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleAsync(x => x.Id == eventId);

        var attemptCount = await verificationDbContext
            .WebhookDeliveryAttempts
            .AsNoTracking()
            .CountAsync(x => x.WebhookEventId == eventId);

        Assert.Equal(WebhookEventStatus.RetryScheduled, storedEvent.Status);
        Assert.NotNull(storedEvent.NextAttemptAt);

        // the previous attempt remains available for diagnostics
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task Retry_WithUnknownEvent_ReturnsNotFound()
    {
        using var response = await _client.PostAsync($"/api/events/{Guid.NewGuid()}/retry", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Retry_WithPendingEvent_ReturnsConflict()
    {
        var endpoint = await _testData.CreateEndpointAsync();

        var eventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "still.pending"}""");

        using var response = await _client.PostAsync($"/api/events/{eventId}/retry", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response
            .Content
            .ReadFromJsonAsync<ProblemDetails>()
            ?? throw new InvalidOperationException("The conflict response body was empty.");

        Assert.Equal(409, problem.Status);
        Assert.Equal("Webhook event cannot be retried.", problem.Title);
        Assert.Contains("current status is Pending", problem.Detail);
    }
}
