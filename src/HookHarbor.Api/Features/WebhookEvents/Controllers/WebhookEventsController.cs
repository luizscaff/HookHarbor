using System.Text.Json;
using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookEvents.Responses;
using HookHarbor.Api.Features.WebhookEvents.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using HookHarbor.Api.Security;

namespace HookHarbor.Api.Features.WebhookEvents.Controllers;

[ApiController]
[Route("in")]
public class WebhookEventsController(HookHarborDbContext dbContext, WebhookEventSignal eventSignal, ILogger<WebhookEventsController> logger) : ControllerBase
{
    private const int MaximumBodySize = 1_048_576; //1 MB

    [HttpPost("{inboundKey}")]
    [EnableRateLimiting("InboundWebhooks")]
    [RequestSizeLimit(MaximumBodySize)]
    public async Task<ActionResult<WebhookEventAcceptedResponse>> Receive(string inboundKey, CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.WebhookEndpoints
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.InboundKey == inboundKey, cancellationToken);

        if (endpoint is null)
        {
            return NotFound();
        }

        await using var stream = new MemoryStream();

        await Request.Body.CopyToAsync(stream, cancellationToken);

        var headers = Request
            .Headers
            .Where(header => !string.Equals(header.Key, ManagementApiKeyMiddleware.HeaderName, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase);

        var serializedHeaders = JsonSerializer.Serialize(headers);

        var webhookEvent = new WebhookEvent(
            endpoint.Id,
            stream.ToArray(),
            Request.ContentType,
            serializedHeaders,
            DateTimeOffset.UtcNow);

        dbContext.WebhookEvents.Add(webhookEvent);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!eventSignal.TryPublish(webhookEvent.Id))
        {
            logger.LogWarning(
                "Webhook event {WebhookEventId} was persisted, but its immediate delivery signal could not be queued. It will be recovered by database polling.",
                webhookEvent.Id);
        }

        return Accepted(WebhookEventAcceptedResponse.FromEntity(webhookEvent));
    }
}
