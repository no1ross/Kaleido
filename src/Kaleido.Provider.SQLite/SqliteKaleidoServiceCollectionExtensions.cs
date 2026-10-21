using Kaleido.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Provider.SQLite;

/// <summary>
/// Registers Kaleido's stores on SQLite, each on its own: the process state
/// (<see cref="AddSqliteContextStore"/>), the storage of Kaleido's built-in outbox
/// (<see cref="AddSqliteOutboxStore"/>) and idempotent requests (<see cref="AddSqliteIdempotencyStore"/>),
/// in one database (ADR 0023).
/// </summary>
/// <remarks>
/// <para>
/// All three share one <see cref="SqliteKaleidoContextDbContext"/>, so they take the same connection
/// string. With the context store and the outbox store both on SQLite, a process-state transition (the
/// state and its events) is one transaction. Several instances may share the database: saves are
/// conditional on the process version, and outbox claims are leased, so each event is relayed by one
/// instance at a time.
/// </para>
/// <para>
/// These calls only register services; they do not open or create the database. The host creates the
/// schema once at startup, e.g.
/// <c>scope.ServiceProvider.GetRequiredService&lt;SqliteKaleidoContextDbContext&gt;().Database.EnsureCreatedAsync()</c>.
/// <c>EnsureCreated</c> never changes an existing database: a database created before a schema change
/// must be recreated or migrated. No health check is registered; a host that wants one adds it, e.g.
/// <c>services.AddHealthChecks().AddDbContextCheck&lt;SqliteKaleidoContextDbContext&gt;("kaleido-sqlite")</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var connectionString = builder.Configuration.GetConnectionString("kaleido");
///
/// builder.Services.AddKaleido(builder.Configuration, o => { /* ... */ })
///     .AddSqliteContextStore(connectionString)
///     .AddSqliteOutboxStore(connectionString)
///     .AddSqliteIdempotencyStore(connectionString)
///     .AddEventPublisher&lt;HttpEventPublisher&gt;();
/// </code>
/// </example>
public static class SqliteKaleidoServiceCollectionExtensions
{
    /// <summary>
    /// Stores process state in SQLite (tables <c>ProcessContexts</c>, ...) and makes process-state
    /// transitions SQLite transactions: the state and its events commit together when the outbox writes
    /// to the same database (<see cref="AddSqliteOutboxStore"/>, or the host's own outbox joining
    /// <see cref="ISqliteKaleidoTransaction"/>).
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="connectionString">The SQLite connection string, e.g. <c>Data Source=kaleido.db</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">
    /// The connection string is invalid (<see cref="SqliteErrorCodes.InvalidConnectionString"/>) or
    /// differs from another SQLite store's (<see cref="SqliteErrorCodes.ConnectionStringMismatch"/>);
    /// another context store or transactions are registered (<c>conflicting_store</c>).
    /// </exception>
    public static IKaleidoBuilder AddSqliteContextStore(this IKaleidoBuilder builder, string connectionString)
    {
        AddDatabase(builder, connectionString);
        builder.Services.TryAddScoped<ISqliteKaleidoTransaction, SqliteKaleidoTransaction>();

        return builder
            .AddContextStore<SqliteKaleidoContextStore>()
            .AddTransactions<SqliteKaleidoTransactions>();
    }

    /// <summary>
    /// Stores Kaleido's built-in outbox in SQLite (table <c>OutboxEvents</c>); Kaleido's relay claims the
    /// entries and hands them to the registered <c>IEventPublisher</c>, retrying across restarts.
    /// Atomic with the process state when the context store is SQLite too (<see cref="AddSqliteContextStore"/>).
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="connectionString">The SQLite connection string; the same as the other SQLite stores'.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">
    /// The connection string is invalid or differs from another SQLite store's; the service uses its own
    /// outbox or direct publication, or another outbox store (<c>conflicting_outbox</c>).
    /// </exception>
    public static IKaleidoBuilder AddSqliteOutboxStore(this IKaleidoBuilder builder, string connectionString)
    {
        AddDatabase(builder, connectionString);

        return builder.AddOutboxStore<SqliteKaleidoOutboxStore>();
    }

    /// <summary>
    /// Stores idempotent requests in SQLite (table <c>IdempotentResults</c>). Independent of the other
    /// stores: it needs no shared transaction.
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="connectionString">The SQLite connection string; the same as the other SQLite stores'.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">
    /// The connection string is invalid or differs from another SQLite store's; another idempotency store
    /// is registered (<c>conflicting_store</c>).
    /// </exception>
    public static IKaleidoBuilder AddSqliteIdempotencyStore(this IKaleidoBuilder builder, string connectionString)
    {
        AddDatabase(builder, connectionString);

        return builder.AddIdempotencyStore<SqliteKaleidoIdempotencyStore>();
    }

    // One database for every SQLite store: the first call registers the DbContext, the others must name
    // the same database.
    private static void AddDatabase(IKaleidoBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        try
        {
            _ = new SqliteConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new KaleidoConfigurationException(
                SqliteErrorCodes.InvalidConnectionString,
                "The SQLite Kaleido store connection string is invalid.",
                exception);
        }

        var registered = builder.Services
            .Select(x => x.ImplementationInstance)
            .OfType<SqliteKaleidoDatabase>()
            .FirstOrDefault();

        if (registered is not null)
        {
            if (!string.Equals(registered.ConnectionString, connectionString, StringComparison.Ordinal))
            {
                throw new KaleidoConfigurationException(
                    SqliteErrorCodes.ConnectionStringMismatch,
                    "The SQLite Kaleido stores were registered with different connection strings. They share one database: pass the same connection string to each.");
            }

            return;
        }

        builder.Services.AddSingleton(new SqliteKaleidoDatabase(connectionString));
        builder.Services.AddDbContext<SqliteKaleidoContextDbContext>(options => options.UseSqlite(connectionString));
    }
}

// The database the SQLite stores were registered on (one per service).
internal sealed record SqliteKaleidoDatabase(string ConnectionString);
