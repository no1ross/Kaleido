using Kaleido.Provider.SQLite.Configuration;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

/// <summary>
/// The EF Core context of the SQLite reference context store: process state, idempotent results
/// and the event outbox. Registered by <c>UseSqliteKaleidoContextStore</c>; its tables are internal
/// to the provider.
/// </summary>
/// <remarks>
/// Registration does not create the database. The host creates the schema once at startup by
/// resolving this context in a scope and calling <c>Database.EnsureCreatedAsync()</c>; without it
/// the first save fails because the tables do not exist. <c>EnsureCreated</c> never changes an
/// existing database, so a database created before a schema change must be recreated or migrated.
/// </remarks>
/// <example>
/// <code>
/// await using (var scope = app.Services.CreateAsyncScope())
/// {
///     await scope.ServiceProvider.GetRequiredService&lt;SqliteKaleidoContextDbContext&gt;().Database.EnsureCreatedAsync();
/// }
/// </code>
/// </example>
/// <param name="options">The context options, configured by <c>UseSqliteKaleidoContextStore</c>.</param>
[ExcludeFromCodeCoverage]
public sealed class SqliteKaleidoContextDbContext(
    DbContextOptions<SqliteKaleidoContextDbContext> options)
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

    internal DbSet<IdempotentResultEntity> IdempotentResults =>
        Set<IdempotentResultEntity>();

    internal DbSet<OutboxEventEntity> OutboxEvents =>
        Set<OutboxEventEntity>();

    /// <inheritdoc/>
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

        modelBuilder.ApplyConfiguration(
            new IdempotentResultEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new OutboxEventEntityConfiguration());
    }
}
