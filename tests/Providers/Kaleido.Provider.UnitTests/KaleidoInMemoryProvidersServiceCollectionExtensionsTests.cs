using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Provider.UnitTests;

public sealed class KaleidoInMemoryProvidersServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    private static IKaleidoBuilder NewBuilder(IServiceCollection? services = null) =>
        (services ?? new ServiceCollection()).AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-service",
            ServiceVersion = "1",
            Assemblies = [typeof(KaleidoInMemoryProvidersServiceCollectionExtensionsTests).Assembly],
        });

    private static ServiceDescriptor Single<TService>(IServiceCollection services) =>
        Assert.Single(services, x => x.ServiceType == typeof(TService));

    [Fact]
    public void AddInMemoryProviders_RegistersAllStoreContractsAsSingletons()
    {
        var builder = NewBuilder();

        Assert.Same(builder, builder.AddInMemoryProviders());
        Assert.Equal(typeof(InMemoryProcessContextStore), Single<IProcessContextStore>(builder.Services).ImplementationType);
        Assert.Equal(typeof(InMemoryProcessIdempotencyStore), Single<IProcessIdempotencyStore>(builder.Services).ImplementationType);
        Assert.Equal(typeof(InMemoryKaleidoOutboxStore), Single<IKaleidoOutboxStore>(builder.Services).ImplementationType);
        Assert.All(builder.Services.Where(x => x.ServiceType == typeof(IProcessContextStore)
            || x.ServiceType == typeof(IProcessIdempotencyStore) || x.ServiceType == typeof(IKaleidoOutboxStore)),
            x => Assert.Equal(ServiceLifetime.Singleton, x.Lifetime));
        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(first.ServiceProvider.GetRequiredService<IProcessContextStore>(), second.ServiceProvider.GetRequiredService<IProcessContextStore>());
        Assert.Same(first.ServiceProvider.GetRequiredService<IProcessIdempotencyStore>(), second.ServiceProvider.GetRequiredService<IProcessIdempotencyStore>());
        Assert.Same(first.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>(), second.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>());
    }

    [Fact]
    public void AddInMemoryProviders_CalledTwice_RegistersOnlyOneOfEach()
    {
        var services = NewBuilder().AddInMemoryProviders().AddInMemoryProviders().Services;

        Single<IProcessContextStore>(services);
        Single<IProcessIdempotencyStore>(services);
        Single<IKaleidoOutboxStore>(services);
    }

    [Fact]
    public void AddInMemoryProviders_DoesNotReplaceAHostChosenStore()
    {
        var services = new ServiceCollection();
        var store = Mock.Of<IKaleidoOutboxStore>();
        services.AddSingleton(store);

        NewBuilder(services).AddInMemoryProviders();

        Assert.Same(store, Single<IKaleidoOutboxStore>(services).ImplementationInstance);
        Single<IProcessContextStore>(services);
        Single<IProcessIdempotencyStore>(services);
    }

    [Fact]
    public void AddInMemoryProviders_AfterDurableStoreChoices_PreservesAllThree()
    {
        var builder = NewBuilder()
            .AddContextStore<DurableContextStore>()
            .AddIdempotencyStore<DurableIdempotencyStore>()
            .AddOutboxStore<DurableOutboxStore>()
            .AddInMemoryProviders();

        Assert.Equal(typeof(DurableContextStore), Single<IProcessContextStore>(builder.Services).ImplementationType);
        Assert.Equal(typeof(DurableIdempotencyStore), Single<IProcessIdempotencyStore>(builder.Services).ImplementationType);
        Assert.Equal(typeof(DurableOutboxStore), Single<IKaleidoOutboxStore>(builder.Services).ImplementationType);
        Assert.All(builder.Services.Where(x => x.ServiceType == typeof(IProcessContextStore)
            || x.ServiceType == typeof(IProcessIdempotencyStore) || x.ServiceType == typeof(IKaleidoOutboxStore)),
            x => Assert.Equal(ServiceLifetime.Scoped, x.Lifetime));
    }

    [Fact]
    public void AddInMemoryProviders_BeforeDurableStoreChoices_AllowsReplacementOfAllThree()
    {
        var builder = NewBuilder().AddInMemoryProviders()
            .AddContextStore<DurableContextStore>()
            .AddIdempotencyStore<DurableIdempotencyStore>()
            .AddOutboxStore<DurableOutboxStore>();

        Assert.Equal(typeof(DurableContextStore), Single<IProcessContextStore>(builder.Services).ImplementationType);
        Assert.Equal(typeof(DurableIdempotencyStore), Single<IProcessIdempotencyStore>(builder.Services).ImplementationType);
        Assert.Equal(typeof(DurableOutboxStore), Single<IKaleidoOutboxStore>(builder.Services).ImplementationType);
        Assert.All(builder.Services.Where(x => x.ServiceType == typeof(IProcessContextStore)
            || x.ServiceType == typeof(IProcessIdempotencyStore) || x.ServiceType == typeof(IKaleidoOutboxStore)),
            x => Assert.Equal(ServiceLifetime.Scoped, x.Lifetime));
    }

    [Fact]
    public void AddInMemoryProviders_NullBuilder_Throws()
    {
        var method = Assert.IsAssignableFrom<MethodInfo>(typeof(KaleidoInMemoryProvidersServiceCollectionExtensions)
            .GetMethod(nameof(KaleidoInMemoryProvidersServiceCollectionExtensions.AddInMemoryProviders)));

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [null]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    private sealed class DurableContextStore : IProcessContextStore
    {
        public Task<ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ProcessContext> SaveAsync(ProcessContext context, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class DurableIdempotencyStore : IProcessIdempotencyStore
    {
        public Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class DurableOutboxStore : IKaleidoOutboxStore
    {
        public Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task RecordRelayAsync(KaleidoOutboxRelay relay, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<KaleidoOutboxStatus> GetStatusAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
