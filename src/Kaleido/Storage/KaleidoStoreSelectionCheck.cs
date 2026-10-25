using Kaleido.Processor.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Storage;

// The explicit choice of AddInMemoryStores(): its presence in the service collection is the choice.
internal sealed class KaleidoInMemoryStoresChoice;

// ADR 0028 (#438): a service never keeps what it must not lose in memory by accident. When the host
// starts, every store the service actually uses must have been chosen - a durable store, or memory
// explicitly (AddInMemoryStores()). The rule reads the final registrations, so the order of the
// Add* calls does not matter, and it covers only the stores in use:
// - the context and idempotency stores, when the service has process steps (a query-only service
//   uses neither);
// - the outbox store, when Kaleido's built-in outbox will write events (an event publisher is
//   registered); the host's own outbox and direct publication use no outbox store.
// Not inferred from the environment name: a production host that forgot is refused the same way.
internal sealed class KaleidoStoreSelectionCheck(IServiceCollection services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var unchosen = InMemoryStoresInUse(services);

        if (unchosen.Count > 0 && !services.Any(x => x.ServiceType == typeof(KaleidoInMemoryStoresChoice)))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.StoresNotChosen,
                $"The service would keep its {string.Join(", ", unchosen)} in memory, which is lost on restart and not shared between instances. " +
                "Register durable stores (e.g. AddSqliteContextStore/AddSqliteIdempotencyStore/AddSqliteOutboxStore, or AddContextStore<T>(), " +
                "AddIdempotencyStore<T>(), AddOutboxStore<T>(), AddOutbox<T>(), AddDirectPublication()), " +
                "or choose memory explicitly with AddInMemoryStores() (development and tests).");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // The stores the service uses that are still Kaleido's in-memory defaults, by what they hold.
    internal static IReadOnlyList<string> InMemoryStoresInUse(IServiceCollection services)
    {
        var inMemory = new List<string>();

        if (services.Any(x => x.ServiceType == typeof(IProcessorStepRegistry)))
        {
            if (Implementation<IKaleidoContextStore>(services) == typeof(InMemoryKaleidoContextStore))
            {
                inMemory.Add("process state (IKaleidoContextStore)");
            }

            if (Implementation<IKaleidoIdempotencyStore>(services) == typeof(InMemoryKaleidoIdempotencyStore))
            {
                inMemory.Add("idempotency records (IKaleidoIdempotencyStore)");
            }
        }

        var writesEvents = Implementation<IEventPublisher>(services) is { } publisher && !typeof(INoOpEventPublisher).IsAssignableFrom(publisher);

        if (writesEvents
            && Implementation<IKaleidoOutbox>(services) == typeof(KaleidoStoreOutbox)
            && Implementation<IKaleidoOutboxStore>(services) == typeof(InMemoryKaleidoOutboxStore))
        {
            inMemory.Add("event outbox (IKaleidoOutboxStore)");
        }

        return inMemory;
    }

    // The implementation the container resolves for a service: its last registration's type or
    // instance type; null for a factory (the host's own, so not an in-memory default).
    private static Type? Implementation<TService>(IServiceCollection services) =>
        services.LastOrDefault(x => x.ServiceType == typeof(TService)) is { } descriptor
            ? descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType()
            : null;
}
