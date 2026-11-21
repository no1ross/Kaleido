using Kaleido.Process.Storage;
using Kaleido.Provider.InMemory;
using Kaleido.Provider.SQLite;
using Kaleido.Server;
using Kaleido.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Hosting.FunctionalTests.Hosting;

public sealed class StoreSelectionFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public void AHostWithProcessStepsAndNoStoreChoice_FailsValidatedDI()
    {
        var exception = Assert.Throws<AggregateException>(() => BuildHost(_ => { }));

        Assert.Contains(nameof(IProcessContextStore), exception.ToString(), StringComparison.Ordinal);
        Assert.Contains(nameof(IKaleidoOutboxStore), exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnExplicitInMemoryChoice_Starts()
    {
        using var host = BuildHost(builder => builder.AddInMemoryProviders());

        await host.StartAsync(Cancel);
        await host.StopAsync(Cancel);
    }

    [Fact]
    public async Task AnExplicitDurableChoice_StartsWithoutAnInMemoryDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaleido-store-selection-{Guid.NewGuid():N}.db");
        try
        {
            using var host = BuildHost(builder => builder.AddSqliteProviders("orders"), path);
            using (var scope = host.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
            }

            await host.StartAsync(Cancel);
            await host.StopAsync(Cancel);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static IHost BuildHost(Action<IKaleidoBuilder> chooseStores, string? path = null) => Host.CreateDefaultBuilder()
        .UseEnvironment("Production")
        .UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        })
        .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:orders"] = $"Data Source={path ?? ":memory:"};Pooling=False",
        }))
        .ConfigureServices((context, services) =>
        {
            var builder = services.AddKaleido(context.Configuration, new()
            {
                ServiceName = "orders",
                ServiceVersion = "1",
                Assemblies = [typeof(ConsoleHostingTests).Assembly],
                TypeFilter = type => type == typeof(ConsoleHostingTests.StartStep) || type == typeof(ConsoleHostingTests.StartStepHandler)
                    || type == typeof(ConsoleHostingTests.LocalSource),
            }).AddHosting(serverOptions: new() { AuthorizationMode = KaleidoAuthorizationMode.None });
            chooseStores(builder);
        })
        .Build();
}
