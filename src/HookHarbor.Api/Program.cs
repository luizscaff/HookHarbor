using HookHarbor.Api.Data;
using Microsoft.EntityFrameworkCore;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Services;
using HookHarbor.Api.Features.WebhookDeliveryAttempts.Workers;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;