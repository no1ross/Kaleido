using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.Infrastructure;

internal sealed class ServiceProjectContextFactory
{
    public string ResolveConnectionString(
        string connectionString)
    {
        var builder =
            new SqliteConnectionStringBuilder(
                connectionString);

        if (!string.IsNullOrWhiteSpace(builder.DataSource)
            && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource =
                Path.GetFullPath(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        builder.DataSource));
        }

        var directory =
            Path.GetDirectoryName(
                builder.DataSource);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return builder.ToString();
    }

    public ServiceProvider CreateSqliteDbContextProvider<TDbContext>(
        string connectionString)
        where TDbContext : DbContext
    {
        var resolvedConnectionString =
            ResolveConnectionString(
                connectionString);

        var services =
            new ServiceCollection();

        services.AddDbContext<TDbContext>(
            options => options.UseSqlite(resolvedConnectionString));

        return services.BuildServiceProvider();
    }
}
