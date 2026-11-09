using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;
using HookHarbor.Api.Features.WebhookEvents;
using Microsoft.EntityFrameworkCore;
using HookHarbor.Api.Features.WebhookEvents.Services;

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
                var eventIds = await GetDeliverableEventIdsAsync(cancellationToken);

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

    private async Task<List<Guid>> GetDeliverableEventIdsAsync(CancellationToken cancellationToken)
    {
        await using var scope =scopeFactory.CreateAsyncScope();

        var claimService = scope.ServiceProvider.GetRequiredService<WebhookEventClaimService>();

        return await claimService.GetDeliverableEventIdsAsync(DateTimeOffset.UtcNow, BatchSize, cancellationToken);
    }

    private async Task DeliverEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<HookHarborDbContext>();
        var deliveryService = scope.ServiceProvider.GetRequiredService<WebhookDeliveryService>();
        var claimService = scope.ServiceProvider.GetRequiredService<WebhookEventClaimService>();

        var claimed = await claimService.TryClaimAsync(eventId, DateTimeOffset.UtcNow, cancellationToken);

        if (!claimed)
        {
            logger.LogDebug("Webhook event {WebhookEventId} was claimed by another worker.", eventId);

            return;
        }

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
