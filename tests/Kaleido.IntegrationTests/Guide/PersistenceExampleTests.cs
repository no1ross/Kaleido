using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Guide.Examples.Persistence;
using Kaleido.Processor;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/persistence.md: the host's own context store, outbox store and transactions on one DbContext.
// A state transition keeps the state and its event together, or neither.
public sealed class PersistenceExampleTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-guide-{Guid.NewGuid():N}.sqlite");

    // The clock of the example's outbox store: leases and retries move only when a test advances it.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    private async Task<ServiceProvider> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton<OrderBook>();   // the guide assembly's getting-started handlers need it
        // ...and its handoff handler a client factory (the returns service is never called here).
        services.AddSingleton<Kaleido.Http.Processor.IKaleidoProcessorClientFactory>(
            _ => throw new NotSupportedException("The persistence tests make no remote calls."));
        OrdersPersistence.AddOrdersPersistence(services, new ConfigurationBuilder().Build(), $"Data Source={_databasePath}");

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.EnsureCreatedAsync(Cancel);

        return provider;
    }

    private static async Task TransitionAsync(ServiceProvider provider, ProcessorContext context)
    {
        await using var scope = provider.CreateAsyncScope();
        var transitionEvent = new KaleidoOutboxEvent { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessCreated, Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow };

        await scope.ServiceProvider.GetRequiredService<IProcessorTransition>().SaveAsync(context, transitionEvent, Cancel);
    }

    private static async Task<(int Processes, int Events)> CountsAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        return (await db.Processes.CountAsync(Cancel), await db.OutboxMessages.CountAsync(Cancel));
    }

    [Fact]
    public async Task AStateTransition_StoresTheStateAndItsEventTogether()
    {
        await using var provider = await BuildAsync();

        await TransitionAsync(provider, new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "orders" });

        Assert.Equal((1, 1), await CountsAsync(provider));
    }

    // The second save of the same new process conflicts: its event is rolled back with it.
    [Fact]
    public async Task AConflictingTransition_KeepsNeitherTheStateNorTheEvent()
    {
        await using var provider = await BuildAsync();
        var process = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "orders" };
        await TransitionAsync(provider, process);

        await Assert.ThrowsAsync<KaleidoConcurrencyException>(() => TransitionAsync(provider, process));

        Assert.Equal((1, 1), await CountsAsync(provider));
    }

    // The stored state loads back, and an update with the loaded version succeeds.
    [Fact]
    public async Task TheContextStore_LoadsWhatItSaved_AndUpdatesByVersion()
    {
        await using var provider = await BuildAsync();
        var created = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "orders" };
        await TransitionAsync(provider, created);

        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IKaleidoContextStore>();
        var loaded = await store.LoadAsync(created.Key, Cancel) ?? throw new InvalidOperationException("Not saved.");
        var updated = await store.SaveAsync(loaded with { State = ProcessExecutionState.Complete }, Cancel);

        Assert.Equal(1, loaded.Version);
        Assert.Equal(2, updated.Version);
        await Assert.ThrowsAsync<KaleidoConcurrencyException>(() => store.SaveAsync(loaded, Cancel));
    }

    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    // Runs one store call in its own scope, as the relay and the health check do.
    private static async Task<T> WithOutboxAsync<T>(ServiceProvider provider, Func<IKaleidoOutboxStore, Task<T>> call)
    {
        await using var scope = provider.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>());
    }

    private Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimAsync(ServiceProvider provider) =>
        WithOutboxAsync(provider, x => x.ClaimEventsAsync(10, Lease, Cancel));

    // #552: the relay's side of the example's outbox store - a claim leases the entry, so it is not
    // claimed again until the lease ends, and a completed entry is gone.
    [Fact]
    public async Task TheOutboxStore_LeasesAClaimedEvent_AndCompletesIt()
    {
        await using var provider = await BuildAsync();
        await TransitionAsync(provider, new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "orders" });

        var claimed = Assert.Single(await ClaimAsync(provider));
        Assert.Empty(await ClaimAsync(provider));
        await WithOutboxAsync(provider, async x => { await x.CompleteEventAsync(claimed.EventId, Cancel); return true; });

        Assert.Equal((1, 0), await CountsAsync(provider));
    }

    // A failed attempt waits for its retry time; the last one parks the event, kept but no longer due.
    [Fact]
    public async Task TheOutboxStore_RetriesAFailedEventAtItsTime_ThenParksIt()
    {
        await using var provider = await BuildAsync();
        await TransitionAsync(provider, new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "orders" });
        var first = Assert.Single(await ClaimAsync(provider));
        await WithOutboxAsync(provider, async x => { await x.FailEventAsync(first.EventId, _time.GetUtcNow().AddMinutes(5), "broker down", Cancel); return true; });

        _time.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        Assert.Empty(await ClaimAsync(provider));
        _time.Advance(TimeSpan.FromTicks(1));
        var retried = Assert.Single(await ClaimAsync(provider));
        await WithOutboxAsync(provider, async x => { await x.FailEventAsync(retried.EventId, retryAtUtc: null, "gave up", Cancel); return true; });

        Assert.Equal(1, retried.Attempts);
        Assert.Equal("broker down", retried.LastError);
        Assert.Equal(0, (await WithOutboxAsync(provider, x => x.GetStatusAsync(_time.GetUtcNow(), Cancel))).DueCount);
        Assert.Equal((1, 1), await CountsAsync(provider));
    }

    // What every service's outbox health check reads: the relay's latest heartbeat (one row per
    // instance) and the due events.
    [Fact]
    public async Task TheOutboxStore_KeepsTheRelaysHeartbeat_AndReportsTheDueEvents()
    {
        await using var provider = await BuildAsync();
        await TransitionAsync(provider, new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "orders" });
        var relay = new KaleidoOutboxRelay { InstanceId = Guid.NewGuid(), ServiceName = "outbox-relay", Publisher = "WarehousePublisher", SeenUtc = _time.GetUtcNow() };
        var before = await WithOutboxAsync(provider, x => x.GetStatusAsync(_time.GetUtcNow(), Cancel));

        await WithOutboxAsync(provider, async x => { await x.RecordRelayAsync(relay, Cancel); return true; });
        _time.Advance(TimeSpan.FromSeconds(30));
        await WithOutboxAsync(provider, async x => { await x.RecordRelayAsync(relay with { SeenUtc = _time.GetUtcNow() }, Cancel); return true; });
        var after = await WithOutboxAsync(provider, x => x.GetStatusAsync(_time.GetUtcNow(), Cancel));

        Assert.Null(before.LastRelaySeenUtc);
        Assert.Equal(_time.GetUtcNow(), after.LastRelaySeenUtc);
        Assert.Equal(1, after.DueCount);
        Assert.NotNull(after.OldestDueUtc);
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().OutboxRelays.CountAsync(Cancel));
    }
}
