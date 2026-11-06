using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;

namespace HookHarbor.Api.Tests;

public sealed class WebhookRetryPolicyTests
{
    [Theory]
    [InlineData(1, null, true)]
    [InlineData(1, 408, true)]
    [InlineData(1, 429, true)]
    [InlineData(1, 500, true)]
    [InlineData(1, 503, true)]
    [InlineData(1, 599, true)]
    [InlineData(1, 200, false)]
    [InlineData(1, 400, false)]
    [InlineData(1, 401, false)]
    [InlineData(1, 404, false)]
    [InlineData(5, null, false)]
    [InlineData(5, 500, false)]
    public void ShouldRetry_ReturnsExpectedResult(int attemptNumber, int? responseStatusCode, bool expected)
    {
        var result = WebhookRetryPolicy.ShouldRetry(attemptNumber, responseStatusCode);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 60)]
    [InlineData(3, 300)]
    [InlineData(4, 1800)]
    public void GetDelay_ReturnsExpectedDelay(int attemptNumber, int expectedSeconds)
    {
        var delay = WebhookRetryPolicy.GetDelay(attemptNumber);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(6)]
    public void GetDelay_WithInvalidAttemptNumber_Throws(int attemptNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WebhookRetryPolicy.GetDelay(attemptNumber));
    }
}
