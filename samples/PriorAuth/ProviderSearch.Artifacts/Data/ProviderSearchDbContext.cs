using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data;

public sealed class ProviderSearchDbContext(
    DbContextOptions<ProviderSearchDbContext> options) : DbContext(options)
{
    public DbSet<Provider> Providers => Set<Provider>();

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
