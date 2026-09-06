using Kaleido.Samples.PriorAuth.History.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.History.Data;

public sealed class HistoryDbContext(
    DbContextOptions<HistoryDbContext> options) : DbContext(options)
{
    public DbSet<PriorAuthRecord> PriorAuthRecords => Set<PriorAuthRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(HistoryDbContext).Assembly);
    }
}
