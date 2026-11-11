using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace HookHarbor.Api.Tests.TestInfrastructure;

public sealed class LowRateLimitWebApplicationFactory : HookHarborWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RateLimits:InboundWebhooks:PermitLimit"] = "3",
                        ["RateLimits:InboundWebhooks:WindowSeconds"] = "60"
                    }
                );
            }

        );
    }
}
