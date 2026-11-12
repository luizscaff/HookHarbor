using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookEvents;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;

public sealed class WebhookDeliveryService(HookHarborDbContext dbContext, HttpClient httpClient, WebhookSignatureService signatureService)
{
    private static readonly HashSet<string> ExcludedForwardingHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Host",
            "Content-Length",
            "Content-Type",
            "Connection",
            "Keep-Alive",
            "Proxy-Authenticate",
            "Proxy-Authorization",
            "TE",
            "Trailer",
            "Transfer-Encoding",
            "Upgrade",
            "X-HookHarbor-Api-Key"
        };

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

        ForwardOriginalHeaders(request, webhookEvent.Headers);

        var signatureTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = signatureService.CreateSignature(webhookEvent.WebhookEndpoint.SigningSecret, signatureTimestamp, webhookEvent.Body);

        request.Headers.TryAddWithoutValidation(WebhookSignatureHeaders.EventId, webhookEvent.Id.ToString());
        request.Headers.TryAddWithoutValidation(WebhookSignatureHeaders.DeliveryId, attempt.Id.ToString());
        request.Headers.TryAddWithoutValidation(WebhookSignatureHeaders.Timestamp, signatureTimestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation(WebhookSignatureHeaders.Signature, signature);

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

            var completedAt = DateTimeOffset.UtcNow;

            attempt.Complete(
                (int)response.StatusCode,
                responseBody,
                completedAt);

            UpdateEventStatus(webhookEvent, attempt, completedAt);
        }
        catch (HttpRequestException exception)
        {
            var completedAt = DateTimeOffset.UtcNow;

            attempt.Fail(exception.Message, completedAt);

            UpdateEventStatus(webhookEvent, attempt, completedAt);
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            var completedAt = DateTimeOffset.UtcNow;

            attempt.Fail($"The destination request timed out: {exception.Message}", completedAt);

            UpdateEventStatus(webhookEvent, attempt, completedAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void UpdateEventStatus(WebhookEvent webhookEvent, WebhookDeliveryAttempt attempt, DateTimeOffset completedAt)
    {
        if (attempt.Succeeded)
        {
            webhookEvent.MarkDelivered();
            return;
        }

        if (WebhookRetryPolicy.ShouldRetry(attempt.AttemptNumber, attempt.ResponseStatusCode))
        {
            var delay = WebhookRetryPolicy.GetDelay(attempt.AttemptNumber);

            webhookEvent.ScheduleRetry(completedAt.Add(delay));

            return;
        }

        webhookEvent.MarkFailed();
    }

    private static void ForwardOriginalHeaders(HttpRequestMessage request, string serializedHeaders)
    {
        var headers = JsonSerializer.Deserialize<Dictionary<string, string[]>>(serializedHeaders) ?? [];

        foreach (var header in headers)
        {
            if (ExcludedForwardingHeaders.Contains(header.Key))
            {
                continue;
            }

            if (request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                continue;
            }

            request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
