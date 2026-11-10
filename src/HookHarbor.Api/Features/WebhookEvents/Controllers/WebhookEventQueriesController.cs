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
}
