using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Kaleido.Eventing;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Server;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Execution;

// #552: the outbox relay as a console host - Host.CreateDefaultBuilder(), no web server, no steps or
// queries, nothing but AddOutboxRelay over the database the services write their events to. Writer
// services post steps over HTTP and only write; the relay hosts publish. Each host is its own container,
// instance and relay, as separate processes would be.
public sealed class OutboxRelayConsoleHostTests : IAsyncLifetime
{
    private const string OrdersService = "orders";
    private const string ReturnsService = "returns";
    private const int StepsPerService = 5;

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-relay-{Guid.NewGuid():N}.db");
    private readonly List<IHost> _hosts = [];
    private TestControlRegistry? _registry;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private string ConnectionString => $"Data Source={_databasePath}";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Stopping waits for each relay to finish, so nothing holds the database file any more.
    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }

        if (_registry is not null)
        {
            await _registry.DisposeAsync();
        }

        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task AConsoleRelayHost_PublishesEveryWritersEvents_Once()
    {
        var orders = await StartWriterAsync(OrdersService);
        var returns = await StartWriterAsync(ReturnsService);
        await PostStepsAsync(orders, OrdersService);
        await PostStepsAsync(returns, ReturnsService);
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");

        var published = new Published();
        await StartRelayAsync(published);
        await WaitUntilAsync(async () => await CountAsync("SELECT COUNT(*) FROM OutboxEvents") == 0);

        Assert.Equal(written, published.Events.Count);
        Assert.Equal(written, published.Events.Select(x => x.EventId).Distinct().Count());
        Assert.Equal([OrdersService, ReturnsService], published.Events.Select(x => x.ServiceName).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(HealthStatus.Healthy, await OutboxHealthAsync(orders));
    }

    // Two relay instances on one database: the leased claims keep an entry in one relay's hands at a
    // time, so every event is published once however the batches interleave; both leave a heartbeat.
    [Fact]
    public async Task TwoConsoleRelayHosts_OnOneDatabase_NeverPublishAnEventTwice()
    {
        var orders = await StartWriterAsync(OrdersService);
        var returns = await StartWriterAsync(ReturnsService);
        await PostStepsAsync(orders, OrdersService);
        await PostStepsAsync(returns, ReturnsService);
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");

        var published = new Published(delay: TimeSpan.FromMilliseconds(20));
        await Task.WhenAll(StartRelayAsync(published, batchSize: 3), StartRelayAsync(published, batchSize: 3));
        await WaitUntilAsync(async () => await CountAsync("SELECT COUNT(*) FROM OutboxEvents") == 0);

        Assert.Equal(written, published.Events.Count);
        Assert.Equal(written, published.Events.Select(x => x.EventId).Distinct().Count());
        Assert.Equal(2, await CountAsync("SELECT COUNT(*) FROM OutboxRelays"));
    }

    // A publisher that keeps failing: each event is retried, then parked - kept in the outbox, never
    // dropped, and no longer due.
    [Fact]
    public async Task AConsoleRelayHost_WhosePublisherKeepsFailing_RetriesThenParksEveryEvent()
    {
        var orders = await StartWriterAsync(OrdersService);
        await PostStepsAsync(orders, OrdersService);
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");

        var published = new Published(alwaysFail: true);
        await StartRelayAsync(published, maxRetryAttempts: 1);
        await WaitUntilAsync(async () => await CountAsync("SELECT COUNT(*) FROM OutboxEvents WHERE IsParked = 1") == written);

        Assert.Equal(written * 2, published.Attempts);
        Assert.Equal(written, await CountAsync("SELECT COUNT(*) FROM OutboxEvents WHERE Attempts = 2"));
        Assert.Empty(published.Events);
    }

    // The console relay: a default host - configuration, logging, hosted services - and no web server.
    private async Task StartRelayAsync(Published published, int batchSize = 20, int maxRetryAttempts = 9)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(new ConsolePublisher(published));
                services.AddKaleido(new ConfigurationBuilder().Build(), new()
                {
                    ServiceName = "outbox-relay",
                    AuthorizationMode = KaleidoAuthorizationMode.None,
                    Assemblies = [typeof(RuntimeRootStep).Assembly],
                    TypeFilter = _ => false,
                    Events = new()
                    {
                        BatchSize = batchSize,
                        Resilience = new() { RetryDelay = TimeSpan.FromMilliseconds(20), MaxRetryAttempts = maxRetryAttempts, CircuitBreakerMinimumThroughput = 1000 },
                    },
                })
                    .AddSqliteOutboxStore(ConnectionString)
                    .AddOutboxRelay<ConsolePublisher>();
            })
            .Build();

        _hosts.Add(host);
        await host.StartAsync(Cancel);
    }

    // A service that only writes its events: no publisher, no relay.
    private async Task<IHost> StartWriterAsync(string serviceName)
    {
        _registry ??= await TestControlRegistry.StartAsync();
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Kaleido:BaseUrl"] = "http://registry.test/",
                    }).Build(), new()
                    {
                        ServiceName = serviceName,
                        ServiceVersion = "1",
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(RuntimeRootStep).Assembly],
                    })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddControlRegistryClient()
                        .AddSqliteContextStore(ConnectionString).AddSqliteOutboxStore(ConnectionString).AddSqliteIdempotencyStore(ConnectionString);
                    services.AddHttpClient("kaleido-registry").ConfigurePrimaryHttpMessageHandler(() => _registry!.CreateHandler());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

        _hosts.Add(host);
        await _registry.WaitForRegistrationAsync(host);

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);

        return host;
    }

    private static async Task PostStepsAsync(IHost writer, string serviceName)
    {
        for (var i = 0; i < StepsPerService; i++)
        {
            using var content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json");
            using var response = await writer.GetTestClient().PostAsync($"/{serviceName}/1/processes/steps/{nameof(RuntimeRootStep).ToLowerInvariant()}", content, Cancel);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task<HealthStatus> OutboxHealthAsync(IHost host) =>
        (await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(x => x.Name == "kaleido-outbox", Cancel)).Status;

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The relay did not finish in time.");
            await Task.Delay(50, Cancel);
        }
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(Cancel))!;
    }

    // What every relay host published, shared by the hosts of a test.
    private sealed class Published(TimeSpan? delay = null, bool alwaysFail = false)
    {
        private int _attempts;

        public ConcurrentQueue<(Guid EventId, string ServiceName)> Events { get; } = new();

        public int Attempts => Volatile.Read(ref _attempts);

        public async Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
            where TEvent : IKaleidoEvent
        {
            Interlocked.Increment(ref _attempts);

            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken);
            }

            if (alwaysFail)
            {
                throw new InvalidOperationException("broker down");
            }

            Events.Enqueue((envelope.Event.EventId, envelope.Context switch
            {
                ProcessEventContext process => process.ServiceName,
                QueryableEventContext query => query.ServiceName,
                _ => string.Empty,
            }));
        }
    }

    private sealed class ConsolePublisher(Published published) : IEventPublisher
    {
        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
            where TEvent : IKaleidoEvent =>
            published.PublishAsync(envelope, cancellationToken);
    }
}
