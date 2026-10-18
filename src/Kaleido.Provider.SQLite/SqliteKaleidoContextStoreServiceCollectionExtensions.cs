using Kaleido.Exceptions;
using Kaleido.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Provider.SQLite;

/// <summary>
/// Registers the SQLite <see cref="IKaleidoContextStore"/>.
/// </summary>
public static class SqliteKaleidoContextStoreServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the in-memory store with SQLite: process state, idempotent results and the event
    /// outbox in one database (tables <c>ProcessContexts</c>, <c>IdempotentResults</c>,
    /// <c>OutboxEvents</c>, ...). Several instances may share the database: saves are conditional on
    /// the process version, and outbox claims are leased, so each event is relayed by one instance at
    /// a time.
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="connectionString">The SQLite connection string, e.g. <c>Data Source=kaleido.db</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">The connection string is invalid.</exception>
    /// <remarks>
    /// This call only registers services; it does not open or create the database. The host
    /// creates the schema once at startup, e.g.
    /// <c>scope.ServiceProvider.GetRequiredService&lt;SqliteKaleidoContextDbContext&gt;().Database.EnsureCreatedAsync()</c>.
    /// <c>EnsureCreated</c> never changes an existing database: a database created before a
    /// schema change (e.g. the <c>OutboxEvents</c> table) must be recreated or migrated.
    /// No health check is registered; a host that wants one adds it, e.g.
    /// <c>services.AddHealthChecks().AddDbContextCheck&lt;SqliteKaleidoContextDbContext&gt;("kaleido-sqlite-context-store")</c>.
    /// </remarks>
    public static IKaleidoBuilder UseSqliteKaleidoContextStore(
        this IKaleidoBuilder builder,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        try
        {
            _ = new SqliteConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new KaleidoConfigurationException(
                SqliteErrorCodes.InvalidConnectionString,
                "The SQLite Kaleido context store connection string is invalid.",
                exception);
        }

        builder.Services.AddDbContext<SqliteKaleidoContextDbContext>(
            options =>
            {
                options.UseSqlite(
                    connectionString);
            });

        builder.Services.RemoveAll<IKaleidoContextStore>();

        builder.Services.AddScoped<
            IKaleidoContextStore,
            SqliteKaleidoContextStore>();

        return builder;
    }
}
