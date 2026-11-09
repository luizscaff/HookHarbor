using HookHarbor.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Features.WebhookEvents.Services;

public sealed class WebhookEventClaimService(HookHarborDbContext dbContext)
{
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(2);

    public async Task<List<Guid>> GetDeliverableEventIdsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var staleBefore = now.Subtract(ProcessingLease);

        return await dbContext.WebhookEvents
            .AsNoTracking()
            .Where(x =>
                x.Status == WebhookEventStatus.Pending
                || (x.Status == WebhookEventStatus.RetryScheduled
                    && x.NextAttemptAt <= now)
                || (x.Status == WebhookEventStatus.Processing
                    && (x.ProcessingStartedAt == null
                        || x.ProcessingStartedAt <= staleBefore)))
            .OrderBy(x => x.NextAttemptAt ?? x.ReceivedAt)
            .Select(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryClaimAsync(Guid eventId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var staleBefore = now.Subtract(ProcessingLease);

        var updatedRows = await dbContext.WebhookEvents
            .Where(x =>
                x.Id == eventId
                && (x.Status == WebhookEventStatus.Pending
                    || (x.Status == WebhookEventStatus.RetryScheduled
                        && x.NextAttemptAt <= now)
                    || (x.Status == WebhookEventStatus.Processing
                        && (x.ProcessingStartedAt == null
                            || x.ProcessingStartedAt <= staleBefore))))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.Status,
                        WebhookEventStatus.Processing)
                    .SetProperty(
                        x => x.ProcessingStartedAt,
                        (DateTimeOffset?)now),
                cancellationToken);

        return updatedRows == 1;
    }
}
