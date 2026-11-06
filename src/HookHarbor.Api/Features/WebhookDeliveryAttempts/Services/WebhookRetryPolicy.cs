namespace HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;

public static class WebhookRetryPolicy
{
    public const int MaximumAttempts = 5;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30)
    ];

    public static bool ShouldRetry(int attemptNumber, int? responseStatusCode)
    {
        if (attemptNumber >= MaximumAttempts)
        {
            return false;
        }

        return responseStatusCode is null or 408 or 429 or >= 500;
    }

    public static TimeSpan GetDelay(int attemptNumber)
    {
        if (attemptNumber < 1 || attemptNumber >= MaximumAttempts)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        }

        return RetryDelays[attemptNumber - 1];
    }
}
