using System.Security.Cryptography;
using HookHarbor.Api.Common;

namespace HookHarbor.Api.Features.WebhookEndpoints;

public sealed class WebhookEndpoint
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string InboundKey { get; private set; } = string.Empty;
    public string DestinationUrl { get; private set; } = string.Empty;
    public string SigningSecret { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private WebhookEndpoint()
    {
        // Required by Entity Framework
    }

    public WebhookEndpoint(string name, string destinationUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Endpoint name is required.", nameof(name));
        }

        if (!Uri.TryCreate(destinationUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Destination must be an absolute HTTP or HTTPS URL", nameof(destinationUrl));
        }

        Id = Guid.NewGuid();
        InboundKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        SigningSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        CreatedAt = DateTimePrecision.TruncateToMicroseconds(DateTimeOffset.UtcNow);

        Update(name, destinationUrl);
    }

    public void Update(string name, string destinationUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Endpoint name is required.", nameof(name));
        }

        if (!Uri.TryCreate(destinationUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Destination must be an absolut HTTP or HTTPS URL.", nameof(destinationUrl));
        }

        Name = name.Trim();
        DestinationUrl = uri.AbsoluteUri;
    }
}