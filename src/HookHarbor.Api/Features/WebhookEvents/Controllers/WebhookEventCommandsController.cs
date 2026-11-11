using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookEvents.Services;
using Microsoft.AspNetCore.Mvc;
using HookHarbor.Api.Features.WebhookEvents.Responses;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Features.WebhookEvents.Controllers;

[ApiController]
[Route("api/events")]
public sealed class WebhookEventCommandsController(
    HookHarborDbContext dbContext,
    WebhookEventSignal eventSignal,
    ILogger<WebhookEventCommandsController> logger) : ControllerBase
{
    [HttpPost("{eventId:guid}/retry")]
    public async Task<ActionResult<WebhookEventRetryAcceptedResponse>> Retry(Guid eventId, CancellationToken cancellationToken)
    {
        var webhookEvent = await dbContext
            .WebhookEvents
            .SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken);

        if (webhookEvent is null)
        {
            return NotFound();
        }

        if (webhookEvent.Status != WebhookEventStatus.Failed)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Webhook event cannot be retried.",
                Detail = $"Only failed webhook events can be manually retried." +
                         $"The current status is {webhookEvent.Status}."
            });
        }

        webhookEvent.ScheduleManualRetry(DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!eventSignal.TryPublish(webhookEvent.Id))
        {
            logger.LogWarning(
                "Webhook event {WebhookEventId} was scheduled for manual retry, " +
                "but its immediate delivery signal could not be queued. " +
                "It will be recovered by database polling.",
                webhookEvent.Id
            );
        }

        return Accepted(new WebhookEventRetryAcceptedResponse(webhookEvent.Id, webhookEvent.Status.ToString(), webhookEvent.NextAttemptAt));
    }
}
