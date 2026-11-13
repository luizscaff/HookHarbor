using System.Text;
using System.Text.Json;
using HookHarbor.Api.Common;
using HookHarbor.Api.Data;
using Microsoft.AspNetCore.Mvc;
using HookHarbor.Api.Features.WebhookEvents.Responses;
using HookHarbor.Api.Features.WebhookEvents.Requests;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Features.WebhookEvents.Controllers;

[ApiController]
public sealed class WebhookEventQueriesController(HookHarborDbContext dbContext) : ControllerBase
{
    [HttpGet("/api/endpoints/{endpointId:guid}/events")]
    public async Task<ActionResult<
            PagedResponse<WebhookEventSummaryResponse>>> ListForEndpoint(
        Guid endpointId,
        [FromQuery] ListWebhookEventsRequest request,
        CancellationToken cancellationToken)
    {
        var endpointExists = await dbContext
            .WebhookEndpoints
            .AsNoTracking()
            .AnyAsync(x => x.Id == endpointId, cancellationToken);

        if (!endpointExists)
        {
            return NotFound();
        }

        var query = dbContext
            .WebhookEvents
            .AsNoTracking()
            .Where(x => x.WebhookEndpointId == endpointId);

        if (request.Status is not null)
        {
            query = query.Where(x => x.Status == request.Status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.ReceivedAt)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new WebhookEventSummaryResponse(
                x.Id,
                x.WebhookEndpointId,
                x.Status.ToString(),
                x.ReceivedAt,
                x.NextAttemptAt,
                dbContext.WebhookDeliveryAttempts.Count(
                    attempt => attempt.WebhookEventId == x.Id)))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<WebhookEventSummaryResponse>(items, request.Page, request.PageSize, totalCount));
    }

    [HttpGet("/api/events/{eventId:guid}")]
    public async Task<ActionResult<WebhookEventDetailResponse>> GetById(Guid eventId, CancellationToken cancellationToken)
    {
        var webhookEvent = await dbContext
            .WebhookEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken);

        if (webhookEvent is null)
        {
            return NotFound();
        }

        var storedAttempts = await dbContext
            .WebhookDeliveryAttempts
            .AsNoTracking()
            .Where(x => x.WebhookEventId == eventId)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync(cancellationToken);

        var attempts = storedAttempts
            .Select(x => new WebhookDeliveryAttemptResponse(
                x.Id,
                x.AttemptNumber,
                x.DestinationUrl,
                x.StartedAt,
                x.CompletedAt,
                x.ResponseStatusCode,
                x.ResponseBody is null ? null : Encoding.UTF8.GetString(x.ResponseBody),
                x.ResponseBodyTruncated,
                x.ErrorMessage,
                x.Succeeded))
            .ToList();

        var headers = JsonSerializer.Deserialize<Dictionary<string, string[]>>(webhookEvent.Headers) ?? [];

        var response = new WebhookEventDetailResponse(
            webhookEvent.Id,
            webhookEvent.WebhookEndpointId,
            webhookEvent.Status.ToString(),
            webhookEvent.ReceivedAt,
            webhookEvent.ContentType,
            headers,
            Encoding.UTF8.GetString(webhookEvent.Body),
            webhookEvent.NextAttemptAt,
            webhookEvent.ProcessingStartedAt,
            attempts);

        return Ok(response);
    }
}
