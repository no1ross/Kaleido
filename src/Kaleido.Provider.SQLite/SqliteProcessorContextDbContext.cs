using Kaleido.Provider.SQLite.Configuration;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

[ExcludeFromCodeCoverage]
public sealed class SqliteProcessorContextDbContext(
    DbContextOptions<SqliteProcessorContextDbContext> options)
    : DbContext(options)
{

    internal DbSet<ProcessorContextEntity> ProcessContexts =>
        Set<ProcessorContextEntity>();

    internal DbSet<ProcessorStepContextEntity> ProcessStepContexts =>
        Set<ProcessorStepContextEntity>();

    internal DbSet<ProcessorAvailableStepEntity> ProcessAvailableSteps =>
        Set<ProcessorAvailableStepEntity>();

    internal DbSet<ProcessorRequiredStepEntity> ProcessRequiredSteps =>
        Set<ProcessorRequiredStepEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new ProcessorContextEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new ProcessorStepContextEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new ProcessorAvailableStepEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new ProcessorRequiredStepEntityConfiguration());
    }
}