using HookHarbor.Api.Features.WebhookEvents.Responses;
using HookHarbor.Api.Tests.TestInfrastructure;
using HookHarbor.Api.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using HookHarbor.Api.Data;
using Microsoft.EntityFrameworkCore;

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
}
