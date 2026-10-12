using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Kaleido.Eventing;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Execution;

// End to end against the SQLite reference store (a real database file): events go to the outbox in
// the same database as the process state, and the relay of each host hands them to the publisher
// until it succeeds - across publisher failures, restarts and several hosts on one database.
public sealed class EventOutboxTests : IAsyncLifetime
{
    private const string RootStepUrl = "/kaleido/processes/steps/runtimerootstep";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-outbox-{Guid.NewGuid():N}.db");

    private readonly List<IHost> _hosts = [];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Stopping waits for each host's relay to finish, so nothing holds the database file any more.
    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }

        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task APublisherThatFailsTwice_GetsEveryEventExactlyOnceAfterRetries()
    {
        var publisher = new RecordingPublisher(failFirst: 2);
        var host = await StartHostAsync(publisher);

        using var response = await PostStepAsync(host.GetTestClient());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WaitUntilAsync(async () => await OutboxCountAsync() == 0);

        Assert.Equal(publisher.Published.Count, publisher.Published.Select(x => x.EventId).Distinct().Count());
        Assert.Equal(publisher.Published.Count + 2, publisher.Calls);
        Assert.Contains(publisher.Published, x => x.EventType == KaleidoEventTypes.ProcessCreated);
        Assert.Contains(publisher.Published, x => x.EventType == KaleidoEventTypes.ProcessStepCompleted);
        Assert.Contains(publisher.Published, x => x.EventType == KaleidoEventTypes.ProcessExecutionCompleted);
    }

    [Fact]
    public async Task EventsThatCouldNotBePublished_SurviveARestartAndArePublishedByTheNextHost()
    {
        var host = await StartHostAsync(new RecordingPublisher(alwaysFail: true), retryDelay: TimeSpan.FromMinutes(10));
        using var response = await PostStepAsync(host.GetTestClient());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var written = await OutboxCountAsync();

        // Every event was attempted (and failed) once; nothing is lost.
        await WaitUntilAsync(async () => await ScalarAsync("SELECT COUNT(*) FROM OutboxEvents WHERE Attempts = 0") is 0L);
        await host.StopAsync(Cancel);

        Assert.True(written > 0);
        Assert.Equal(written, await OutboxCountAsync());

        // The retry time has come (instead of waiting ten minutes).
        await ExecuteAsync("UPDATE OutboxEvents SET NextAttemptUtc = 0");

        var publisher = new RecordingPublisher();
        await StartHostAsync(publisher);

        await WaitUntilAsync(async () => await OutboxCountAsync() == 0);
        Assert.Equal(written, publisher.Published.Count);
    }

    [Fact]
    public async Task TwoHostsOnOneDatabase_PublishEveryEventExactlyOnce()
    {
        var first = new RecordingPublisher();
        var second = new RecordingPublisher();
        var firstHost = await StartHostAsync(first);
        var secondHost = await StartHostAsync(second);

        for (var i = 0; i < 5; i++)
        {
            using var a = await PostStepAsync(firstHost.GetTestClient());
            using var b = await PostStepAsync(secondHost.GetTestClient());
        }

        await WaitUntilAsync(async () => await OutboxCountAsync() == 0);

        var all = first.Published.Concat(second.Published).Select(x => x.EventId).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.True(all.Count >= 20);
    }

    [Fact]
    public async Task AQuery_PublishesItsQueryExecutedEventThroughTheOutbox()
    {
        var publisher = new RecordingPublisher();
        var host = await StartHostAsync(publisher);

        using var content = new StringContent("""{ "query": {} }""", Encoding.UTF8, "application/json");
        using var response = await host.GetTestClient().PostAsync("/kaleido/queryable/functionalrecordcontextsource/query", content, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WaitUntilAsync(() => Task.FromResult(publisher.Published.Any(x => x.EventType == KaleidoEventTypes.QueryExecuted)));
        Assert.Equal(0L, await OutboxCountAsync());
    }

    // Like the PriorAuth services: the host creates the store's schema at startup.
    private async Task<IHost> StartHostAsync(RecordingPublisher publisher, TimeSpan? retryDelay = null)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<FunctionalRecordData>();
                    services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                        {
                            o.ServiceName = FunctionalProcessorNames.TestProcessor;
                            o.Assemblies = [typeof(RuntimeRootStep).Assembly];
                            o.Events.Resilience.RetryDelay = retryDelay ?? TimeSpan.FromMilliseconds(50);
                        })
                        .AddHttp()
                        .UseSqliteKaleidoContextStore($"Data Source={_databasePath}");
                    services.RemoveAll<IEventPublisher>();
                    services.AddSingleton<IEventPublisher>(publisher);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

        _hosts.Add(host);

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);

        return host;
    }

    private static async Task<HttpResponseMessage> PostStepAsync(HttpClient client)
    {
        using var content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json");
        return await client.PostAsync(RootStepUrl, content, Cancel);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The outbox did not drain in time.");
            await Task.Delay(50, Cancel);
        }
    }

    private async Task<long> OutboxCountAsync() =>
        (long)(await ScalarAsync("SELECT COUNT(*) FROM OutboxEvents"))!;

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(Cancel);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Cancel);
    }

    // Records what it was handed; fails the first calls (or every call) on request.
    private sealed class RecordingPublisher(int failFirst = 0, bool alwaysFail = false) : IEventPublisher
    {
        private int _calls;

        public ConcurrentQueue<(Guid EventId, string EventType)> Published { get; } = new();

        public int Calls => Volatile.Read(ref _calls);

        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent
        {
            var call = Interlocked.Increment(ref _calls);

            if (alwaysFail || call <= failFirst)
            {
                return Task.FromException(new InvalidOperationException("broker down"));
            }

            Published.Enqueue((envelope.Event.EventId, envelope.EventType));
            return Task.CompletedTask;
        }
    }
}
