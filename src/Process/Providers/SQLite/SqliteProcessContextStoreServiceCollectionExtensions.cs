using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kaleido.Process.Participant.Context;

namespace Kaleido.Process.Providers.SQLite;

public static class SqliteProcessContextStoreServiceCollectionExtensions
{
    public static IParticipantBuilder UseSqliteProcessContextStore(
        this IParticipantBuilder builder,
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
