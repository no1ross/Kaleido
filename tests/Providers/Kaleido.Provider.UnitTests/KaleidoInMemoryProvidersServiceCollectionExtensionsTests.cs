using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Provider.UnitTests;

public sealed class KaleidoInMemoryProvidersServiceCollectionExtensionsTests : SutFixture
{
    private static IKaleidoBuilder NewBuilder(IServiceCollection? services = null) =>
        (services ?? new ServiceCollection()).AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-service",
            Assemblies = [typeof(KaleidoInMemoryProvidersServiceCollectionExtensionsTests).Assembly],
        });

    private static ServiceDescriptor Single<TService>(IServiceCollection services) =>
        Assert.Single(services, x => x.ServiceType == typeof(TService));

    [Fact]
    public void AddInMemoryProviders_RegistersAllStoreContractsAsSingletons()
    {
        var builder = NewBuilder();

        Assert.Same(builder, builder.AddInMemoryProviders());
        Assert.Equal(typeof(InMemoryKaleidoContextStore), Single<IKaleidoContextStore>(builder.Services).ImplementationType);
        Assert.Equal(typeof(InMemoryKaleidoIdempotencyStore), Single<IKaleidoIdempotencyStore>(builder.Services).ImplementationType);
        Assert.Equal(typeof(InMemoryKaleidoOutboxStore), Single<IKaleidoOutboxStore>(builder.Services).ImplementationType);
        Assert.All(builder.Services.Where(x => x.ServiceType == typeof(IKaleidoContextStore) ||
            x.ServiceType == typeof(IKaleidoIdempotencyStore) || x.ServiceType == typeof(IKaleidoOutboxStore)),
            x => Assert.Equal(ServiceLifetime.Singleton, x.Lifetime));
    }

    [Fact]
    public void AddInMemoryProviders_CalledTwice_RegistersOnlyOneOfEach()
    {
        var services = NewBuilder().AddInMemoryProviders().AddInMemoryProviders().Services;

        Single<IKaleidoContextStore>(services);
        Single<IKaleidoIdempotencyStore>(services);
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
        Single<IKaleidoContextStore>(services);
        Single<IKaleidoIdempotencyStore>(services);
    }

    [Fact]
    public void AddInMemoryProviders_NullBuilder_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddInMemoryProviders());
    }
}
