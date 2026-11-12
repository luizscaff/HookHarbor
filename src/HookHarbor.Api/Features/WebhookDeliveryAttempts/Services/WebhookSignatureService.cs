using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;

public sealed class WebhookSignatureService
{
    public string CreateSignature(string signingSecret, long timestamp, byte[] body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingSecret);

        ArgumentNullException.ThrowIfNull(body);

        var timestampBytes = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".");

        var signedPayload = new byte[timestampBytes.Length + body.Length];

        timestampBytes.CopyTo(signedPayload, index: 0);

        body.CopyTo(signedPayload, index: timestampBytes.Length);

        var key = Encoding.UTF8.GetBytes(signingSecret);

        var hash = HMACSHA256.HashData(key, signedPayload);

        return $"v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
