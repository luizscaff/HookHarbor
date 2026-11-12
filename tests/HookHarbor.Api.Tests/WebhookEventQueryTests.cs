using HookHarbor.Api.Features.WebhookEvents.Responses;
using HookHarbor.Api.Tests.TestInfrastructure;
using HookHarbor.Api.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using HookHarbor.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;

namespace HookHarbor.Api.Tests;

public sealed class WebhookEventQueryTests : IClassFixture<HookHarborWebApplicationFactory>
{
    private readonly HookHarborWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly WebhookTestData _testData;

    public WebhookEventQueryTests(HookHarborWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _testData = new WebhookTestData(_client);
    }

    [Fact]
    public async Task ListForEndpoint_WithEvents_ReturnsPaginatedEvents()
    {
        var endpoint = await _testData.CreateEndpointAsync();

        await _testData.CreateEventAsync(endpoint.InboundKey, """{"number": 1}""");
        await _testData.CreateEventAsync(endpoint.InboundKey, """{"number": 2}""");
        await _testData.CreateEventAsync(endpoint.InboundKey, """{"number": 3}""");

        using var response = await _client.GetAsync($"/api/endpoints/{endpoint.Id}/events?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response
            .Content
            .ReadFromJsonAsync<PagedResponse<WebhookEventSummaryResponse>>()
            ?? throw new InvalidOperationException("The event-list response body was empty.");

        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);

        Assert.All(result.Items, item => Assert.Equal(endpoint.Id, item.WebhookEndpointId));
    }

    [Fact]
    public async Task ListForEndpoint_WithSecondPage_ReturnsRemainingEvent()
    {
        var endpoint = await _testData.CreateEndpointAsync();

        await _testData.CreateEventAsync(endpoint.InboundKey, """{"number": 1}""");
        await _testData.CreateEventAsync(endpoint.InboundKey, """{"number": 2}""");
        await _testData.CreateEventAsync(endpoint.InboundKey, """{"number": 3}""");

        using var response = await _client.GetAsync($"/api/endpoints/{endpoint.Id}/events?page=2&pagesize=2");

        response.EnsureSuccessStatusCode();

        var result = await response
            .Content
            .ReadFromJsonAsync<PagedResponse<WebhookEventSummaryResponse>>()
            ?? throw new InvalidOperationException("The event-list response body was empty.");

        Assert.Equal(2, result.Page);
        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ListForEndpoint_WithStatusFilter_ReturnsMatchingEvents()
    {
        var endpoint = await _testData.CreateEndpointAsync();

        var failedEventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "pending"}""");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();

            var failedEvent = await dbContext.WebhookEvents.SingleAsync(x => x.Id == failedEventId);

            failedEvent.MarkFailed();

            await dbContext.SaveChangesAsync();
        }

        using var response = await _client.GetAsync($"/api/endpoints/{endpoint.Id}/events?status=Failed");

        response.EnsureSuccessStatusCode();

        var result = await response
            .Content
            .ReadFromJsonAsync<PagedResponse<WebhookEventSummaryResponse>>()
            ?? throw new InvalidOperationException("The event-list response body was empty.");

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(failedEventId, result.Items[0].Id);
        Assert.Equal("Failed", result.Items[0].Status);
    }

    [Fact]
    public async Task ListForEndpoint_DoesNotReturnAnotherEndpointEvents()
    {
        var firstEndpoint = await _testData.CreateEndpointAsync();
        var secondEndpoint = await _testData.CreateEndpointAsync();

        await _testData.CreateEventAsync(firstEndpoint.InboundKey, """{"endpoint": 1}""");
        await _testData.CreateEventAsync(secondEndpoint.InboundKey, """{"endpoint": 2}""");

        using var response = await _client.GetAsync($"/api/endpoints/{firstEndpoint.Id}/events");

        response.EnsureSuccessStatusCode();

        var result = await response
            .Content
            .ReadFromJsonAsync<PagedResponse<WebhookEventSummaryResponse>>()
            ?? throw new InvalidOperationException("The event-list response body was empty.");

        Assert.Single(result.Items);
        Assert.Equal(firstEndpoint.Id, result.Items[0].WebhookEndpointId);
    }

    [Fact]
    public async Task ListForEndpoint_WithUnknownEndpoint_ReturnsNotFound()
    {
        using var response = await _client.GetAsync($"/api/endpoints/{Guid.NewGuid()}/events");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListForEndpoint_WithInvalidPagination_ReturnsBadRequest(int page, int pageSize)
    {
        var endpoint = await _testData.CreateEndpointAsync();

        using var response = await _client.GetAsync($"/api/endpoints/{endpoint.Id}/events" + $"?page={page}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithExistingEvent_ReturnsEventDetails()
    {
        const string body =
            """
            {
                "type": "payment.succeeded",
                "paymentId": "pay_123"
            }
            """;

        var endpoint = await _testData.CreateEndpointAsync();

        var eventId = await _testData.CreateEventAsync(
            endpoint.InboundKey,
            body,
            new Dictionary<string, string> { ["X-Webhook-Source"] = "manual-test" });

        using var response = await _client.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response
            .Content
            .ReadFromJsonAsync<WebhookEventDetailResponse>()
            ?? throw new InvalidOperationException("The event-detail response body was empty.");

        Assert.Equal(eventId, result.Id);
        Assert.Equal(endpoint.Id, result.WebhookEndpointId);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("application/json; charset=utf-8", result.ContentType);
        Assert.Equal(body, result.Body);
        Assert.Null(result.NextAttemptAt);
        Assert.Null(result.ProcessingStartedAt);
        Assert.Empty(result.Attempts);
        Assert.True(result.Headers.TryGetValue("X-Webhook-Source", out var sourceHeader));
        Assert.Contains("manual-test", sourceHeader);
    }

    [Fact]
    public async Task GetById_WithUnknownEvent_ReturnsNotFound()
    {
        using var response = await _client.GetAsync($"/api/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AfterSuccessfulDelivery_ReturnsDeliveryAttempt()
    {
        const string destinationUrl = "https://destination.example.com/webhooks";

        var endpoint = await _testData.CreateEndpointAsync(destinationUrl);

        var eventId = await _testData.CreateEventAsync(endpoint.InboundKey, """{"type": "payment.succeeded"}""");

        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"received": true}""", Encoding.UTF8, "application/json")
                }));

        using var deliveryHttpClient = new HttpClient(handler);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope
                .ServiceProvider
                .GetRequiredService<HookHarborDbContext>();

            var deliveryService = new WebhookDeliveryService(dbContext, deliveryHttpClient, new WebhookSignatureService());

            await deliveryService.DeliverAsync(eventId, CancellationToken.None);
        }

        using var response = await _client.GetAsync($"/api/events/{eventId}");

        response.EnsureSuccessStatusCode();

        var result = await response
            .Content
            .ReadFromJsonAsync<WebhookEventDetailResponse>()
            ?? throw new InvalidOperationException("The event-detail response body was empty.");

        Assert.Equal("Delivered", result.Status);

        var attempt = Assert.Single(result.Attempts);

        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(destinationUrl, attempt.DestinationUrl);
        Assert.Equal(200, attempt.ResponseStatusCode);
        Assert.Equal("""{"received": true}""", attempt.ResponseBody);
        Assert.True(attempt.Succeeded);
        Assert.NotNull(attempt.CompletedAt);
        Assert.Null(attempt.ErrorMessage);
    }
}
