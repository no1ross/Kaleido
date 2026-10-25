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

    // What AddKaleido registers: the in-memory defaults, the built-in outbox and the no-op publisher;
    // with process steps, the step registry.
    private static ServiceCollection Defaults(bool withProcesses = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventPublisher, EventPublisher>();
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
        Assert.DoesNotContain("event outbox", exception.Message, StringComparison.Ordinal);
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

    // A query-only service keeps neither process state nor idempotency records.
    [Fact]
    public void AQueryOnlyService_UsesNoContextOrIdempotencyStore()
    {
        Assert.Empty(KaleidoStoreSelectionCheck.InMemoryStoresInUse(Defaults(withProcesses: false)));
    }

    [Fact]
    public void WithoutAnyPublisherRegistration_NoOutboxStoreIsInUse()
    {
        var services = Defaults(withProcesses: false);
        services.RemoveAll<IEventPublisher>();

        Assert.Empty(KaleidoStoreSelectionCheck.InMemoryStoresInUse(services));
    }

    // The built-in outbox writes events only when a publisher is registered.
    [Fact]
    public void WithAPublisher_TheBuiltInOutboxStoreIsInUse()
    {
        var services = Defaults(withProcesses: false);
        services.AddSingleton(Mock.Of<IEventPublisher>());

        Assert.Equal(["event outbox (IKaleidoOutboxStore)"], KaleidoStoreSelectionCheck.InMemoryStoresInUse(services));
    }

    [Fact]
    public void WithAPublisher_ADurableOutboxStore_OrAnotherOutbox_NeedsNoChoice()
    {
        var durable = Defaults(withProcesses: false);
        durable.AddSingleton(Mock.Of<IEventPublisher>());
        durable.AddScoped(_ => Mock.Of<IKaleidoOutboxStore>());

        var direct = Defaults(withProcesses: false);
        direct.AddSingleton(Mock.Of<IEventPublisher>());
        direct.AddScoped<IKaleidoOutbox, KaleidoDirectPublicationOutbox>();

        Assert.Empty(KaleidoStoreSelectionCheck.InMemoryStoresInUse(durable));
        Assert.Empty(KaleidoStoreSelectionCheck.InMemoryStoresInUse(direct));
    }

    private sealed class FakeIdempotencyStore : IKaleidoIdempotencyStore
    {
        public Task<Kaleido.Processor.Context.IdempotentResult?> AdmitAsync(Kaleido.Processor.Context.IdempotentResult result, CancellationToken cancellationToken) => Task.FromResult<Kaleido.Processor.Context.IdempotentResult?>(null);

        public Task CompleteAsync(Kaleido.Processor.Context.IdempotentResult result, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
