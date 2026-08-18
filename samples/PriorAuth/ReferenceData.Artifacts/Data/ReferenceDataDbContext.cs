using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;

public sealed class ReferenceDataDbContext(
    DbContextOptions<ReferenceDataDbContext> options) : DbContext(options)
{
    public DbSet<State> States => Set<State>();

    public DbSet<ZipCode> ZipCodes => Set<ZipCode>();

    public DbSet<Plan> Plans => Set<Plan>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ReferenceDataDbContext).Assembly);
    }
}
