using Kaleido.Provider.SQLite.Configuration;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

public sealed class SqliteProcessContextDbContext(
    DbContextOptions<SqliteProcessContextDbContext> options)
    : DbContext(options)
{

    public DbSet<ProcessContextEntity> ProcessContexts =>
        Set<ProcessContextEntity>();

    public DbSet<ProcessStepContextEntity> ProcessStepContexts =>
        Set<ProcessStepContextEntity>();

    public DbSet<ProcessAvailableStepEntity> ProcessAvailableSteps =>
        Set<ProcessAvailableStepEntity>();

    public DbSet<ProcessRequiredStepEntity> ProcessRequiredSteps =>
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