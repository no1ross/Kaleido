using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Provider.SQLite;

public static class SqliteProcessorContextStoreServiceCollectionExtensions
{
    public static IKaleidoBuilder UseSqliteProcessorContextStore(
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
                "The SQLite process context store connection string is invalid.",
                exception);
        }

        builder.Services.AddDbContext<SqliteProcessorContextDbContext>(
            options =>
            {
                options.UseSqlite(
                    connectionString);
            });

        builder.Services.RemoveAll<IProcessorContextStore>();

        builder.Services.AddScoped<
            IProcessorContextStore,
            SqliteProcessorContextStore>();

        // Register a health check for the process context store so consumers
        // get liveness/readiness coverage automatically. The check verifies
        // that the underlying SQLite database can be reached. Expose the
        // endpoint in your app with app.MapHealthChecks("/health").
        builder.Services
            .AddHealthChecks()
            .AddDbContextCheck<SqliteProcessorContextDbContext>(
                name: "kaleido-sqlite-process-context-store");

        return builder;
    }
}
