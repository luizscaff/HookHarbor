using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;
using HookHarbor.Api.Features.WebhookEvents;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Features.WebhookDeliveryAttempts.Workers;

public sealed class WebhookDeliveryWorker(IServiceScopeFactory scopeFactory, ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    private const int BatchSize = 20;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Webhook delivery worker started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var eventIds = await GetPendingEventIdsAsync(cancellationToken);

                foreach (var eventId in eventIds)
                {
                    await DeliverEventAsync(eventId, cancellationToken);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch(Exception e)
            {
                logger.LogError(e, "An error occurred while polling webhook events.");
            }

            try
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Webhook delivery worker stopped.");
    }

    private async Task<List<Guid>> GetPendingEventIdsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();
        
        var now = DateTimeOffset.UtcNow;

        return await dbContext
            .WebhookEvents
            .AsNoTracking()
            .Where(x => x.Status == WebhookEventStatus.Pending || (x.Status == WebhookEventStatus.RetryScheduled && x.NextAttemptAt <= now))
            .OrderBy(x => x.NextAttemptAt ?? x.ReceivedAt)
            .Select(x => x.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task DeliverEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var deliveryService = scope.ServiceProvider.GetRequiredService<WebhookDeliveryService>();

        try
        {
            await deliveryService.DeliverAsync(eventId, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Delivery of webhook event {WebhookEventId} failed unexpectedly.", eventId);
        }
    }
}
