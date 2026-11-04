using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookEvents;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;

public sealed class WebhookDeliveryService(HookHarborDbContext dbContext, HttpClient httpClient)
{
    public async Task DeliverAsync(Guid webhookEventId, CancellationToken cancellationToken)
    {
        var webhookEvent = await dbContext
            .WebhookEvents
            .Include(x => x.WebhookEndpoint)
            .SingleOrDefaultAsync(x => x.Id == webhookEventId, cancellationToken);

        if (webhookEvent is null)
        {
            throw new InvalidOperationException($"Webhook event ' {webhookEventId} ' was not found.");
        }

        if (webhookEvent.Status == WebhookEventStatus.Delivered)
        {
            throw new InvalidOperationException(
                "A delivered webhook event cannot be delivered again.");
        }

        var previousAttemptCount = await dbContext
            .WebhookDeliveryAttempts
            .CountAsync(x => x.WebhookEventId == webhookEventId, cancellationToken);

        var attempt = new WebhookDeliveryAttempt(
            webhookEvent.Id,
            previousAttemptCount + 1,
            DateTimeOffset.UtcNow,
            webhookEvent.WebhookEndpoint.DestinationUrl);

        dbContext.WebhookDeliveryAttempts.Add(attempt);

        //save before sending, so the started attempt is recorded even if the application stops while waiting for the destination
        await dbContext.SaveChangesAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, attempt.DestinationUrl);

        request.Content = new ByteArrayContent(webhookEvent.Body);

        if (!string.IsNullOrWhiteSpace(webhookEvent.ContentType))
        {
            request.Content.Headers.TryAddWithoutValidation("Content-Type", webhookEvent.ContentType);
        }

        try
        {
            using var response = await httpClient
                .SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            var responseBody = await response
                .Content
                .ReadAsByteArrayAsync(cancellationToken);

            attempt.Complete(
                (int)response.StatusCode,
                responseBody,
                DateTimeOffset.UtcNow);

            if (attempt.Succeeded)
            {
                webhookEvent.MarkDelivered();
            }
            else
            {
                webhookEvent.MarkFailed();
            }
        }
        catch (HttpRequestException exception)
        {
            attempt.Fail(exception.Message, DateTimeOffset.UtcNow);

            webhookEvent.MarkFailed();
        } catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            attempt.Fail($"The destination request timed out: {exception.Message}", DateTimeOffset.UtcNow);

            webhookEvent.MarkFailed();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
