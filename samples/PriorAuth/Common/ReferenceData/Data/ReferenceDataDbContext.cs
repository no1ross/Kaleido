using Kaleido.Samples.PriorAuth.ReferenceData.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Data;

public sealed class ReferenceDataDbContext(
    DbContextOptions<ReferenceDataDbContext> options) : DbContext(options)
{
    public DbSet<State> States => Set<State>();

    public DbSet<ZipCode> ZipCodes => Set<ZipCode>();

    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<Network> Networks => Set<Network>();

    public DbSet<PlanNetwork> PlanNetworks => Set<PlanNetwork>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ReferenceDataDbContext).Assembly);
    }
}
