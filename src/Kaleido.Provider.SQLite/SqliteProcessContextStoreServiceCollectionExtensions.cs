using Kaleido.Process.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Provider.SQLite;

public static class SqliteProcessContextStoreServiceCollectionExtensions
{
    public static IKaleidoBuilder UseSqliteContextStore(
        this IKaleidoBuilder builder,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        builder.Services.AddDbContext<SqliteProcessContextDbContext>(
            options =>
            {
                options.UseSqlite(
                    connectionString);
            });

        builder.Services.RemoveAll<IProcessContextStore>();

        builder.Services.AddScoped<
            IProcessContextStore,
            SqliteProcessContextStore>();

        // Register a health check for the process context store so consumers
        // get liveness/readiness coverage automatically. The check verifies
        // that the underlying SQLite database can be reached. Expose the
        // endpoint in your app with app.MapHealthChecks("/health").
        builder.Services
            .AddHealthChecks()
            .AddDbContextCheck<SqliteProcessContextDbContext>(
                name: "kaleido-sqlite-process-context-store");

        return builder;
    }
}
