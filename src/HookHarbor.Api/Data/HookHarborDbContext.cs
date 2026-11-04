using HookHarbor.Api.Features.WebhookDeliveryAttempts;
using HookHarbor.Api.Features.WebhookEndpoints;
using HookHarbor.Api.Features.WebhookEvents;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Data;

public sealed class HookHarborDbContext(DbContextOptions<HookHarborDbContext> options) : DbContext(options)
{
    public DbSet<WebhookDeliveryAttempt> WebhookDeliveryAttempts => Set<WebhookDeliveryAttempt>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HookHarborDbContext).Assembly);
    }
}