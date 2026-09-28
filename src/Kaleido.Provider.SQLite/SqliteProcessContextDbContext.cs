using Kaleido.Provider.SQLite.Configuration;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

[ExcludeFromCodeCoverage]
public sealed class SqliteProcessContextDbContext(
    DbContextOptions<SqliteProcessContextDbContext> options)
    : DbContext(options)
{

    internal DbSet<ProcessContextEntity> ProcessContexts =>
        Set<ProcessContextEntity>();

    internal DbSet<ProcessStepContextEntity> ProcessStepContexts =>
        Set<ProcessStepContextEntity>();

    internal DbSet<ProcessAvailableStepEntity> ProcessAvailableSteps =>
        Set<ProcessAvailableStepEntity>();

    internal DbSet<ProcessRequiredStepEntity> ProcessRequiredSteps =>
        Set<ProcessRequiredStepEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new ProcessContextEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new ProcessStepContextEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new ProcessAvailableStepEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new ProcessRequiredStepEntityConfiguration());
    }
}