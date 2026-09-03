using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kaleido.Process.Context;

namespace Kaleido.Process.Providers.SQLite;

public static class SqliteProcessContextStoreServiceCollectionExtensions
{
    public static IProcessorBuilder UseSqliteProcessContextStore(
        this IProcessorBuilder builder,
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

        return builder;
    }
}
