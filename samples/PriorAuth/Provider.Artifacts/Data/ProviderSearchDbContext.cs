using Kaleido.Samples.PriorAuth.Provider.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Provider.Data;

public sealed class ProviderSearchDbContext(
    DbContextOptions<ProviderSearchDbContext> options) : DbContext(options)
{
    public DbSet<ProviderInfo> Providers => Set<ProviderInfo>();

    public DbSet<ProviderIdentifier> ProviderIdentifiers => Set<ProviderIdentifier>();

    public DbSet<ProviderLocation> ProviderLocations => Set<ProviderLocation>();

    public DbSet<ProviderLocationNetwork> ProviderLocationNetworks => Set<ProviderLocationNetwork>();

    public DbSet<ProviderLocationSpecialty> ProviderLocationSpecialties => Set<ProviderLocationSpecialty>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ProviderSearchDbContext).Assembly);
    }
}
