using Kaleido.Exceptions;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Provider.SQLite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.IntegrationTests;

// ADR 0028 (#438), on a real host: a service with process steps that chose no stores does not start;
// the explicit in-memory choice and durable stores do. The guide's orders service has steps.
public sealed class StoreSelectionTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-stores-{Guid.NewGuid():N}.db");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    private static IHost Host(Action<IKaleidoBuilder> stores) =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<OrderBook>();
                stores(services.AddKaleido(new ConfigurationBuilder().Build(), new()
                {
                    ServiceName = "orders",
                    Assemblies = [typeof(OrderSource).Assembly],
                    AuthorizationMode = KaleidoAuthorizationMode.None,
                }));
            })
            .Build();

    [Fact]
    public async Task AHostThatChoseNoStores_DoesNotStart()
    {
        using var host = Host(_ => { });

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => host.StartAsync(Cancel));

        Assert.Equal(ConfigurationErrorCodes.StoresNotChosen, exception.Code);
    }

    [Fact]
    public async Task TheExplicitInMemoryChoice_Starts()
    {
        using var host = Host(kaleido => kaleido.AddInMemoryStores());

        await host.StartAsync(Cancel);
        await host.StopAsync(Cancel);
    }

    [Fact]
    public async Task DurableStores_Start_WithoutTheInMemoryChoice()
    {
        var connection = $"Data Source={_databasePath}";
        using var host = Host(kaleido => kaleido.AddSqliteContextStore(connection).AddSqliteIdempotencyStore(connection));

        await host.StartAsync(Cancel);
        await host.StopAsync(Cancel);
    }
}
