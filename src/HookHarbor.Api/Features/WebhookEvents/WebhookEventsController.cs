using System.Text.Json;
using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookEvents.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Features.WebhookEvents;

[ApiController]
[Route("in")]
public class WebhookEventsController(HookHarborDbContext dbContext) : ControllerBase
{
    private const int MaximumBodySize = 1_048_576; //1 MB

    [HttpPost("{inboundKey}")]
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

        var headers = Request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var serializedHeaders = JsonSerializer.Serialize(headers);

        var webhookEvent = new WebhookEvent(
            endpoint.Id,
            stream.ToArray(),
            Request.ContentType,
            serializedHeaders,
            DateTimeOffset.UtcNow);

        dbContext.WebhookEvents.Add(webhookEvent);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Accepted(WebhookEventAcceptedResponse.FromEntity(webhookEvent));
    }
}
