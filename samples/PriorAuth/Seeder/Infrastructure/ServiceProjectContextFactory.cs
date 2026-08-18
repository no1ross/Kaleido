using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.Infrastructure;

internal sealed class ServiceProjectContextFactory
{
    public string GetServiceProjectPath(
        string projectName)
    {
        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                projectName));
    }

    public IConfiguration BuildServiceConfiguration(
        string serviceProjectPath)
    {
        return new ConfigurationBuilder()
            .SetBasePath(serviceProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();
    }

    public string ResolveConnectionString(
        IConfiguration configuration,
        string connectionStringName,
        string fallbackConnectionString,
        string serviceProjectPath)
    {
        var connectionString =
            configuration.GetConnectionString(connectionStringName)
            ?? fallbackConnectionString;

        var builder =
            new SqliteConnectionStringBuilder(
                connectionString);

        if (!string.IsNullOrWhiteSpace(builder.DataSource)
            && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource =
                Path.GetFullPath(
                    Path.Combine(
                        serviceProjectPath,
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
        string serviceProjectName,
        string connectionStringName,
        string fallbackConnectionString)
        where TDbContext : DbContext
    {
        var serviceProjectPath =
            GetServiceProjectPath(
                serviceProjectName);

        var configuration =
            BuildServiceConfiguration(
                serviceProjectPath);

        var connectionString =
            ResolveConnectionString(
                configuration,
                connectionStringName,
                fallbackConnectionString,
                serviceProjectPath);

        var services =
            new ServiceCollection();

        services.AddDbContext<TDbContext>(
            options => options.UseSqlite(connectionString));

        return services.BuildServiceProvider();
    }
}
