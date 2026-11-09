using Kaleido.Provider.SQLite.Configuration;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

/// <summary>
/// The EF Core context of the SQLite control registry store (#553): registrations, instances and each
/// service's current version, in the registry host's own database. Registered by
/// <c>AddSqliteRegistryStore</c>; its tables are internal to the provider.
/// </summary>
/// <remarks>
/// Registration does not create the database. The registry host creates the schema once at startup by
/// resolving this context in a scope and calling <c>Database.EnsureCreatedAsync()</c>. <c>EnsureCreated</c>
/// never changes an existing database. Several registry nodes may share one database file on one
/// machine; SQLite on a network share is unsafe, so registry nodes on several machines need a server
/// database (a store of the host's own, <c>AddRegistryStore&lt;T&gt;()</c>).
/// </remarks>
/// <param name="options">The context options, configured by <c>AddSqliteRegistryStore</c>.</param>
[ExcludeFromCodeCoverage]
public sealed class SqliteKaleidoRegistryDbContext(
    DbContextOptions<SqliteKaleidoRegistryDbContext> options)
    : DbContext(options)
{
    internal DbSet<RegistryRegistrationEntity> RegistryRegistrations => Set<RegistryRegistrationEntity>();

    internal DbSet<RegistryInstanceEntity> RegistryInstances => Set<RegistryInstanceEntity>();

    internal DbSet<RegistryCurrentVersionEntity> RegistryCurrentVersions => Set<RegistryCurrentVersionEntity>();

    internal DbSet<RegistryWatchEntity> RegistryWatch => Set<RegistryWatchEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RegistryRegistrationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RegistryInstanceEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RegistryCurrentVersionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RegistryWatchEntityConfiguration());
    }
}
