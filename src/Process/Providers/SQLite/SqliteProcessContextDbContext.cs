using Kaleido.Process.Providers.SQLite.Configuration;
using Kaleido.Process.Providers.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Process.Providers.SQLite;

public sealed class SqliteProcessContextDbContext
    : DbContext
{
    public SqliteProcessContextDbContext(
        DbContextOptions<SqliteProcessContextDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProcessContextEntity> ProcessContexts =>
        Set<ProcessContextEntity>();

    public DbSet<ProcessStepContextEntity> ProcessStepContexts =>
        Set<ProcessStepContextEntity>();

    public DbSet<ProcessAvailableStepEntity> ProcessAvailableSteps =>
        Set<ProcessAvailableStepEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new ProcessContextEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new ProcessStepContextEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new ProcessAvailableStepEntityConfiguration());
    }
}