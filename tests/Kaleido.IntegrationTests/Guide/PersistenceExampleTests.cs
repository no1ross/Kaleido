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

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/persistence.md: the host's own context store, outbox and transactions on one DbContext.
// A state transition keeps the state and its event together, or neither.
public sealed class PersistenceExampleTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-guide-{Guid.NewGuid():N}.sqlite");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    private async Task<ServiceProvider> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<OrderBook>();   // the guide assembly's getting-started handlers need it
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
        var processId = Guid.NewGuid();
        await TransitionAsync(provider, new ProcessorContext { ProcessId = processId, ProcessorName = "orders" });

        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IKaleidoContextStore>();
        var loaded = await store.LoadAsync(processId, Cancel) ?? throw new InvalidOperationException("Not saved.");
        var updated = await store.SaveAsync(loaded with { State = ProcessExecutionState.Complete }, Cancel);

        Assert.Equal(1, loaded.Version);
        Assert.Equal(2, updated.Version);
        await Assert.ThrowsAsync<KaleidoConcurrencyException>(() => store.SaveAsync(loaded, Cancel));
    }
}
