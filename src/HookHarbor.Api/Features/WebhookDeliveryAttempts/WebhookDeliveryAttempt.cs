using HookHarbor.Api.Common;
using HookHarbor.Api.Features.WebhookEvents;

namespace HookHarbor.Api.Features.WebhookDeliveryAttempts;

public sealed class WebhookDeliveryAttempt
{
    public Guid Id { get; private set; }
    public Guid WebhookEventId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public byte[]? ResponseBody { get; private set; }
    public bool ResponseBodyTruncated { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool Succeeded { get; private set; }
    public string DestinationUrl { get; private set; } = string.Empty;
    public WebhookEvent WebhookEvent { get; private set; } = null!;

    private WebhookDeliveryAttempt()
    {
    }

    public WebhookDeliveryAttempt(
        Guid webhookEventId,
        int attemptNumber,
        DateTimeOffset startedAt,
        string destinationUrl)
    {
        if (webhookEventId == Guid.Empty)
        {
            throw new ArgumentException("Webhook event ID is required.", nameof(webhookEventId));
        }

        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number must be greater than zero.");
        }

        if (!Uri.TryCreate(destinationUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Destination must be an absolute HTTP or HTTPS URL.", nameof(destinationUrl));
        }

        Id = Guid.NewGuid();
        WebhookEventId = webhookEventId;
        AttemptNumber = attemptNumber;
        StartedAt = DateTimePrecision.TruncateToMicroseconds(startedAt);
        DestinationUrl = destinationUrl;
    }

    public void Complete(
        int responseStatusCode,
        byte[] responseBody,
        DateTimeOffset completedAt,
        bool responseBodyTruncated = false)
    {
        EnsureNotCompleted();

        if (responseStatusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseStatusCode),
                "Response status code must be between 100 and 599.");
        }

        ArgumentNullException.ThrowIfNull(responseBody);

        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        ResponseBodyTruncated = responseBodyTruncated;
        CompletedAt =
            DateTimePrecision.TruncateToMicroseconds(completedAt);
        Succeeded = responseStatusCode is >= 200 and <= 299;
    }

    public void Fail(string errorMessage, DateTimeOffset completedAt)
    {
        EnsureNotCompleted();

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("An error message is required.", nameof(errorMessage));
        }

        ErrorMessage = errorMessage;
        CompletedAt = DateTimePrecision.TruncateToMicroseconds(completedAt);
        Succeeded = false;
    }

    private void EnsureNotCompleted()
    {
        if (CompletedAt is not null)
        {
            throw new InvalidOperationException("The delivery attempt has already been completed.");
        }
    }
}