using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data;

public sealed class ConfigurationDbContext(
    DbContextOptions<ConfigurationDbContext> options) : DbContext(options)
{
    public DbSet<ProcedureModalityRule> ProcedureModalityRules => Set<ProcedureModalityRule>();

    public DbSet<MriProcedureCodeRule> MriProcedureCodeRules => Set<MriProcedureCodeRule>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ConfigurationDbContext).Assembly);
    }
}
