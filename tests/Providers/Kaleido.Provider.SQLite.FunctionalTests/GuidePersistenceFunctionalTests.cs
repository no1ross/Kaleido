using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Guide.Examples.Persistence;
using Kaleido.Process.Client;
using Kaleido.Process.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Provider.SQLite.FunctionalTests;

public sealed class GuidePersistenceFunctionalTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-guide-persistence-{Guid.NewGuid():N}.sqlite");
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }

    private async Task<ServiceProvider> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(time);
        services.AddSingleton<OrderBook>();
        services.AddSingleton<IKaleidoProcessorClientFactory>(_ => throw new NotSupportedException("No remote calls in persistence tests."));
        OrdersPersistence.AddOrdersPersistence(services, new ConfigurationBuilder().Build(), $"Data Source={databasePath};Pooling=False");
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.EnsureCreatedAsync(Cancel);
        return provider;
    }

    private static ProcessContext NewProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ProcessorName = "orders",
        ProcessorVersion = "1",
        State = ProcessExecutionState.Active,
    };

    private static async Task TransitionAsync(ServiceProvider provider, ProcessContext context)
    {
        await using var scope = provider.CreateAsyncScope();
        await using var transaction = await scope.ServiceProvider.GetRequiredService<IProcessTransactions>().BeginAsync(Cancel);
        await scope.ServiceProvider.GetRequiredService<IProcessContextStore>().SaveAsync(context, Cancel);
        await scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>().AddAsync(
            [new KaleidoOutboxEvent
            {
                EventId = Guid.NewGuid(),
                EventType = ProcessEventTypes.ProcessCreated,
                Payload = "{}",
                ServiceName = "orders",
                ServiceVersion = "1",
                OccurredUtc = DateTimeOffset.UtcNow,
            }], Cancel);
        await transaction.CommitAsync(Cancel);
    }

    private static async Task<(int Processes, int Events)> CountsAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        return (await db.Processes.CountAsync(Cancel), await db.OutboxMessages.CountAsync(Cancel));
    }

    private static async Task<T> WithOutboxAsync<T>(ServiceProvider provider, Func<IKaleidoOutboxStore, Task<T>> call)
    {
        await using var scope = provider.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>());
    }

    private static Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimAsync(ServiceProvider provider) =>
        WithOutboxAsync(provider, store => store.ClaimEventsAsync(10, Lease, Cancel));

    [Fact]
    public async Task AStateTransition_StoresTheStateAndItsEventTogether()
    {
        await using var provider = await BuildAsync();

        await TransitionAsync(provider, NewProcess());

        Assert.Equal((1, 1), await CountsAsync(provider));
    }

    [Fact]
    public async Task AConflictingTransition_KeepsNeitherTheStateNorTheEvent()
    {
        await using var provider = await BuildAsync();
        var process = NewProcess();
        await TransitionAsync(provider, process);

        await Assert.ThrowsAsync<ProcessConcurrencyException>(() => TransitionAsync(provider, process));

        Assert.Equal((1, 1), await CountsAsync(provider));
    }

    [Fact]
    public async Task TheContextStore_LoadsWhatItSaved_AndUpdatesByVersion()
    {
        await using var provider = await BuildAsync();
        var created = NewProcess();
        await TransitionAsync(provider, created);

        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessContextStore>();
        var loaded = await store.LoadAsync(created.Key, Cancel) ?? throw new InvalidOperationException("Not saved.");
        var updated = await store.SaveAsync(loaded with { State = ProcessExecutionState.Complete }, Cancel);

        Assert.Equal(1, loaded.Version);
        Assert.Equal(2, updated.Version);
        await Assert.ThrowsAsync<ProcessConcurrencyException>(() => store.SaveAsync(loaded, Cancel));
    }

    [Fact]
    public async Task TheOutboxStore_LeasesAClaimedEvent_AndCompletesIt()
    {
        await using var provider = await BuildAsync();
        await TransitionAsync(provider, NewProcess());

        var claimed = Assert.Single(await ClaimAsync(provider));
        Assert.Empty(await ClaimAsync(provider));
        await WithOutboxAsync(provider, async store => { await store.CompleteEventAsync(claimed.EventId, Cancel); return true; });

        Assert.Equal((1, 0), await CountsAsync(provider));
    }

    [Fact]
    public async Task TheOutboxStore_RetriesAFailedEventAtItsTime_ThenParksIt()
    {
        await using var provider = await BuildAsync();
        await TransitionAsync(provider, NewProcess());
        var first = Assert.Single(await ClaimAsync(provider));
        await WithOutboxAsync(provider, async store => { await store.FailEventAsync(first.EventId, time.GetUtcNow().AddMinutes(5), "broker down", Cancel); return true; });

        time.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        Assert.Empty(await ClaimAsync(provider));
        time.Advance(TimeSpan.FromTicks(1));
        var retried = Assert.Single(await ClaimAsync(provider));
        await WithOutboxAsync(provider, async store => { await store.FailEventAsync(retried.EventId, retryAtUtc: null, "gave up", Cancel); return true; });

        Assert.Equal(1, retried.Attempts);
        Assert.Equal("broker down", retried.LastError);
        Assert.Equal(0, (await WithOutboxAsync(provider, store => store.GetStatusAsync(time.GetUtcNow(), Cancel))).DueCount);
        Assert.Equal((1, 1), await CountsAsync(provider));
    }

    [Fact]
    public async Task TheOutboxStore_KeepsTheRelaysHeartbeat_AndReportsTheDueEvents()
    {
        await using var provider = await BuildAsync();
        await TransitionAsync(provider, NewProcess());
        var relay = new KaleidoOutboxRelay { InstanceId = Guid.NewGuid(), ServiceName = "outbox-relay", Publisher = "WarehousePublisher", SeenUtc = time.GetUtcNow() };
        var before = await WithOutboxAsync(provider, store => store.GetStatusAsync(time.GetUtcNow(), Cancel));

        await WithOutboxAsync(provider, async store => { await store.RecordRelayAsync(relay, Cancel); return true; });
        time.Advance(TimeSpan.FromSeconds(30));
        await WithOutboxAsync(provider, async store => { await store.RecordRelayAsync(relay with { SeenUtc = time.GetUtcNow() }, Cancel); return true; });
        var after = await WithOutboxAsync(provider, store => store.GetStatusAsync(time.GetUtcNow(), Cancel));

        Assert.Null(before.LastRelaySeenUtc);
        Assert.Equal(time.GetUtcNow(), after.LastRelaySeenUtc);
        Assert.Equal(1, after.DueCount);
        Assert.NotNull(after.OldestDueUtc);
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().OutboxRelays.CountAsync(Cancel));
    }
}
