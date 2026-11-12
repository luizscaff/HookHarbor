using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;
using System.Text;

namespace HookHarbor.Api.Tests;

public sealed class WebhookSignatureServiceTests
{
    [Fact]
    public void CreateSignature_WithKnownValues_ReturnsExpectedSignature()
    {
        const string secret = "test_signing_secret";
        const long timestamp = 1_731_000_000;
        
        var body = Encoding.UTF8.GetBytes("""{"type":"payment.succeeded"}""");

        var service = new WebhookSignatureService();

        var signature = service.CreateSignature(secret, timestamp, body);

        Assert.Equal("v1=c575502abdc415ac68023639746683b65c091fdb3012257f73ca2fd51f8709e0", signature);
    }
}
