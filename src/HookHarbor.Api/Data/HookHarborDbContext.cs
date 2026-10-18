using HookHarbor.Api.Features.WebhookEndpoints;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Data;

public sealed class HookHarborDbContext(DbContextOptions<HookHarborDbContext> options) : DbContext(options)
{
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HookHarborDbContext).Assembly);
    }
}