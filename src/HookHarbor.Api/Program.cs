using HookHarbor.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Configuration;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Workers;
using HookHarbor.Api.Features.WebhookEvents.Configuration;
using HookHarbor.Api.Features.WebhookEvents.Services;
using HookHarbor.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<HookHarborDbContext>(
    (serviceProvider, options) =>
    {
        var configuration = serviceProvider
            .GetRequiredService<IConfiguration>();

        var connectionString =
            configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'Database' was not found.");

        options.UseNpgsql(connectionString);
    });

builder.Services.AddHttpClient<WebhookDeliveryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<WebhookDeliveryWorker>();
}

builder.Services.AddScoped<WebhookEventClaimService>();

builder.Services.AddSingleton<WebhookEventSignal>();
builder.Services.AddSingleton<WebhookSignatureService>();

builder.Services
    .AddOptions<ManagementApiKeyOptions>()
    .Bind(builder.Configuration.GetSection(ManagementApiKeyOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "Management API key must be configured.")
    .ValidateOnStart();

builder.Services
    .AddOptions<InboundWebhookRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(InboundWebhookRateLimitOptions.SectionName))
    .Validate(options => options.PermitLimit > 0, "Inbound webhook permit limit must be greater than zero.")
    .Validate(options => options.WindowSeconds > 0, "Inbound webhook rate-limit window must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("InboundWebhooks", httpContext =>
    {
        var rateLimitOptions = httpContext
            .RequestServices
            .GetRequiredService<IOptions<InboundWebhookRateLimitOptions>>()
            .Value;

        var partitionKey = httpContext
            .Connection
            .RemoteIpAddress
            ?.ToString()
            ?? "unknown";

        return RateLimitPartition
            .GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }
            );
    });
});

builder.Services
    .AddOptions<WebhookDeliveryOptions>()
    .Bind(builder.Configuration.GetSection(WebhookDeliveryOptions.SectionName))
    .Validate(options => options.MaximumCapturedResponseBodyBytes > 0, "Maximum captured response body size must be greater than zero.")
    .ValidateOnStart();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<ManagementApiKeyMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;