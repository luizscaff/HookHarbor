using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;
using HookHarbor.Api.Features.WebhookEvents;
using HookHarbor.Api.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;

namespace HookHarbor.Api.Tests;

public sealed class WebhookDeliveryServiceTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HookHarborWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly WebhookTestData _testData;

    public WebhookDeliveryServiceTests(HookHarborWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _testData = new WebhookTestData(_client);
    }

    [Fact]
    public async Task DeliverAsync_WithSuccessfulResponse_MarksEventDelivered()
    {
        const string destinationUrl = "https://destination.example.com/webhooks";
        const string originalBody =
            """
            {
                "type": "payment.succeeded",
                "customerId": 123
            }
            """;

        var endpoint = await _testData.CreateEndpointAsync(destinationUrl);
        var webhookEventId = await _testData.CreateEventAsync(endpoint.InboundKey, originalBody);

        string? deliveredBody = null;
        string? deliveredContentType = null;
        Uri? deliveredUri = null;

        var handler = new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                deliveredUri = request.RequestUri;

                var content = request.Content ?? throw new InvalidOperationException("The delivered request had no content");

                deliveredBody = await content.ReadAsStringAsync(cancellationToken);

                deliveredContentType = request.Content.Headers.ContentType?.ToString();

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"received": true}""",
                        Encoding.UTF8,
                        "application/json"
                    )
                };
            }
        );

        using var httpClient = new HttpClient(handler);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var service = new WebhookDeliveryService(dbContext, httpClient);

        await service.DeliverAsync(webhookEventId, CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var storedEvent = await dbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleAsync(x => x.Id == webhookEventId);

        var attempt = await dbContext
            .WebhookDeliveryAttempts
            .AsNoTracking()
            .SingleAsync(x => x.WebhookEventId == webhookEventId);

        Assert.Equal(WebhookEventStatus.Delivered, storedEvent.Status);

        Assert.True(attempt.Succeeded);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(200, attempt.ResponseStatusCode);
        Assert.NotNull(attempt.CompletedAt);
        Assert.Null(attempt.ErrorMessage);
        Assert.Equal(destinationUrl, deliveredUri?.ToString());
        Assert.Equal(originalBody, deliveredBody);

        Assert.StartsWith("application/json", deliveredContentType);
    }

    [Fact]
    public async Task DeliverAsync_WithServerError_SchedulesRetry()
    {
        const string destinationUrl = "https://destination.example.com/webhooks";

        var endpoint = await _testData.CreateEndpointAsync(destinationUrl);
        var webhookEventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "test"}""");

        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error": "Temporarily unavailable"}""", Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var service = new WebhookDeliveryService(dbContext, httpClient);

        await service.DeliverAsync(webhookEventId, CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var storedEvent = await dbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleAsync(x => x.Id == webhookEventId);

        var attempt = await dbContext
            .WebhookDeliveryAttempts
            .AsNoTracking()
            .SingleAsync(x => x.WebhookEventId == webhookEventId);

        Assert.Equal(WebhookEventStatus.RetryScheduled, storedEvent.Status);
        Assert.NotNull(storedEvent.NextAttemptAt);
        Assert.False(attempt.Succeeded);
        Assert.Equal(500, attempt.ResponseStatusCode);
        Assert.NotNull(attempt.CompletedAt);
        Assert.Null(attempt.ErrorMessage);
        Assert.Equal("""{"error": "Temporarily unavailable"}""", Encoding.UTF8.GetString(attempt.ResponseBody!));
    }

    [Fact]
    public async Task DeliverAsync_WithTransportError_SchedulesRetryAndRecordsError()
    {
        const string destinationUrl = "https://destination.example.com/webhooks";

        var endpoint = await _testData.CreateEndpointAsync(destinationUrl);
        var webhookEventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "test"}""");

        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("Connection refused"));

        using var httpClient = new HttpClient(handler);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var service = new WebhookDeliveryService(dbContext, httpClient);

        await service.DeliverAsync(webhookEventId, CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var storedEvent = await dbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleAsync(x => x.Id == webhookEventId);

        var attempt = await dbContext
            .WebhookDeliveryAttempts
            .AsNoTracking()
            .SingleAsync(x => x.WebhookEventId == webhookEventId);

        Assert.Equal(WebhookEventStatus.RetryScheduled, storedEvent.Status);
        Assert.NotNull(storedEvent.NextAttemptAt);
        Assert.False(attempt.Succeeded);
        Assert.Null(attempt.ResponseStatusCode);
        Assert.Null(attempt.ResponseBody);
        Assert.NotNull(attempt.CompletedAt);
        Assert.Contains("Connection refused", attempt.ErrorMessage);
    }

    [Fact]
    public async Task DeliverAsync_WithUnknownEvent_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var httpClient = new HttpClient(handler);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var service = new WebhookDeliveryService(dbContext, httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeliverAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Contains("was not found", exception.Message);
    }

    [Fact]
    public async Task DeliverAsync_WithBadRequest_MarksEventFailed()
    {
        const string destinationUrl = "https://destination.example.com/webhooks";

        var endpoint = await _testData.CreateEndpointAsync(destinationUrl);
        var webhookEventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "test"}""");

        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        using var httpClient = new HttpClient(handler);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var service = new WebhookDeliveryService(dbContext, httpClient);

        await service.DeliverAsync(webhookEventId, CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var storedEvent = await dbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleAsync(x => x.Id ==  webhookEventId);

        Assert.Null(storedEvent.NextAttemptAt);
        Assert.Equal(WebhookEventStatus.Failed, storedEvent.Status);
    }

    [Fact]
    public async Task DeliverAsync_AfterRetriableFailure_SucceedsOnSecondAttempt()
    {
        const string destinationUrl = "https://destination.example.com/webhooks";

        var endpoint = await _testData.CreateEndpointAsync(destinationUrl);
        var webhookEventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "test"}""");

        var requestCount = 0;

        var handler = new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("""{"error": "Unavailable"}""", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"received": true}""", Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var service = new WebhookDeliveryService(dbContext, httpClient);

        await service.DeliverAsync(webhookEventId, CancellationToken.None);

        await service.DeliverAsync(webhookEventId, CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        var storedEvent = await dbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleAsync(x => x.Id == webhookEventId);

        var attempts = await dbContext
            .WebhookDeliveryAttempts
            .AsNoTracking()
            .Where(x => x.WebhookEventId == webhookEventId)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync();

        Assert.Equal(WebhookEventStatus.Delivered, storedEvent.Status);
        Assert.Null(storedEvent.NextAttemptAt);
        Assert.Equal(2, attempts.Count);

        Assert.Equal(1, attempts[0].AttemptNumber);
        Assert.False(attempts[0].Succeeded);
        Assert.Equal(503, attempts[0].ResponseStatusCode);

        Assert.Equal(2, attempts[1].AttemptNumber);
        Assert.True(attempts[1].Succeeded);
        Assert.Equal(200, attempts[1].ResponseStatusCode);
    }

    [Fact]
    public async Task DeliverAsync_ForwardsProviderHeadersButExcludesInternalHeaders()
    {
        const string destinationUrl = "https://destination.example.com/webhooks";
        const string stripeSignature = "t=1731000000,v1=test-signature";

        var endpoint = await _testData.CreateEndpointAsync(destinationUrl);

        var eventId = await _testData.CreateEventAsync(
            endpoint.InboundKey,
            """{"type": "payment.succeeded"}""",
            new Dictionary<string, string>
            {
                ["Stripe-Signature"] = stripeSignature,
                ["X-Custom-Provider-Header"] = "custom-value"
            }
        );

        string? deliveredStripeSignature = null;
        string? deliveredCustomHeader = null;
        bool managementApiKeyWasForwarded = false;

        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                if (request.Headers.TryGetValues("Stripe-Signature", out var signatureValues))
                {
                    deliveredStripeSignature = signatureValues.Single();
                }

                if (request.Headers.TryGetValues("X-Custom-Provider-Header", out var customValues))
                {
                    deliveredCustomHeader = customValues.Single();
                }

                managementApiKeyWasForwarded = request.Headers.Contains("X-HookHarbor-Api-Key") || request.Content?.Headers.Contains("X-HookHarbor-Api-Key") == true;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        );

        using var deliveryHttpClient = new HttpClient(handler);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

        var service = new WebhookDeliveryService(dbContext, deliveryHttpClient);

        await service.DeliverAsync(eventId, CancellationToken.None);

        Assert.Equal(stripeSignature, deliveredStripeSignature);
        Assert.Equal("custom-value", deliveredCustomHeader);
        Assert.False(managementApiKeyWasForwarded);
    }
}
