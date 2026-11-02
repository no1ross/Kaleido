using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Processor.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Storage.UnitTests;

// ADR 0028 (#438): a store the service uses is never left in memory by accident.
public sealed class KaleidoStoreSelectionCheckTests
    : SutFixture
{
    private static KaleidoStoreSelectionCheck CreateSut(IServiceCollection services) => new(services);

    // What AddKaleido registers: the in-memory defaults and the outbox; with process steps, the step
    // registry.
    private static ServiceCollection Defaults(bool withProcesses = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKaleidoContextStore, InMemoryKaleidoContextStore>();
        services.AddSingleton<IKaleidoIdempotencyStore, InMemoryKaleidoIdempotencyStore>();
        services.AddScoped<IKaleidoOutbox, KaleidoStoreOutbox>();
        services.AddSingleton<IKaleidoOutboxStore, InMemoryKaleidoOutboxStore>();

        if (withProcesses)
        {
            services.AddSingleton(Mock.Of<IProcessorStepRegistry>());
        }

        return services;
    }

    private static Task StartAsync(IServiceCollection services) =>
        CreateSut(services).StartAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task AServiceWithProcesses_LeftAtTheDefaults_DoesNotStart_NamingTheStores()
    {
        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(Defaults()));

        Assert.Equal(ConfigurationErrorCodes.StoresNotChosen, exception.Code);
        Assert.Contains("process state (IKaleidoContextStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("idempotency records (IKaleidoIdempotencyStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("event outbox (IKaleidoOutboxStore)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddInMemoryStores()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheExplicitInMemoryChoice_Starts()
    {
        var services = Defaults();
        services.AddSingleton<KaleidoInMemoryStoresChoice>();

        await StartAsync(services);
        await CreateSut(services).StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DurableStores_Start()
    {
        var services = Defaults();
        services.AddScoped(_ => Mock.Of<IKaleidoContextStore>());
        services.AddScoped<IKaleidoIdempotencyStore, FakeIdempotencyStore>();
        services.AddScoped(_ => Mock.Of<IKaleidoOutboxStore>());

        await StartAsync(services);
    }

    [Fact]
    public async Task OneStoreLeftInMemory_IsTheOneNamed()
    {
        var services = Defaults();
        services.AddScoped<IKaleidoIdempotencyStore, FakeIdempotencyStore>();

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(services));

        Assert.Contains("process state", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotency records", exception.Message, StringComparison.Ordinal);
    }

    // A query-only service keeps neither process state nor idempotency records, but it writes its events
    // (#552): its outbox store must be chosen too.
    [Fact]
    public void AQueryOnlyService_UsesOnlyTheOutboxStore()
    {
        Assert.Equal(["event outbox (IKaleidoOutboxStore)"], KaleidoStoreSelectionCheck.InMemoryStoresInUse(Defaults(withProcesses: false)));
    }

    // Whether or not this host publishes: every service writes its events (#552).
    [Fact]
    public void ADurableOutboxStore_NeedsNoChoice()
    {
        var durable = Defaults(withProcesses: false);
        durable.AddScoped(_ => Mock.Of<IKaleidoOutboxStore>());

        Assert.Empty(KaleidoStoreSelectionCheck.InMemoryStoresInUse(durable));
    }
    private sealed class FakeIdempotencyStore : IKaleidoIdempotencyStore
    {
        public Task<IdempotentResult?> AdmitAsync(IdempotentResult result, CancellationToken cancellationToken) => Task.FromResult<IdempotentResult?>(null);

        public Task<bool> CompleteAsync(IdempotentResult result, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
