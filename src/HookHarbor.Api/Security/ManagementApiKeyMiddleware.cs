using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace HookHarbor.Api.Security;

public sealed class ManagementApiKeyMiddleware(RequestDelegate next, IOptions<ManagementApiKeyOptions> options)
{
    public const string HeaderName = "X-HookHarbor-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var suppliedApiKey = context.Request.Headers[HeaderName].ToString();

        if (!ApiKeysMatch(options.Value.ApiKey, suppliedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            context.Response.Headers.WWWAuthenticate = "ApiKey";

            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = $"A valie {HeaderName} header is required."
                },
                context.RequestAborted);

            return;
        }

        await next(context);
    }

    private static bool ApiKeysMatch(string expectedApiKey, string suppliedApiKey)
    {
        if (string.IsNullOrEmpty(suppliedApiKey))
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedApiKey));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedApiKey));

        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
