using System.Reflection;
using Kaleido.Http.Registry;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoRegistrySnapshotServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static FakeKaleidoBuilder CreateBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IDistributedCache>());

        return new FakeKaleidoBuilder(services);
    }

    private static IRegistrySnapshotStore Resolve(IServiceCollection services) =>
        services
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true })
            .GetRequiredService<IRegistrySnapshotStore>();

    [Fact]
    public void AddDistributedRegistrySnapshots_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddDistributedRegistrySnapshots());
    }

    [Fact]
    public void AddDistributedRegistrySnapshots_ReplacesAStoreRegisteredBefore()
    {
        var builder = CreateBuilder();
        builder.Services.AddSingleton<IRegistrySnapshotStore, InMemoryRegistrySnapshotStore>();

        builder.AddDistributedRegistrySnapshots();

        Assert.Single(builder.Services, d => d.ServiceType == typeof(IRegistrySnapshotStore) && !d.IsKeyedService);
        Assert.IsType<DistributedRegistrySnapshotStore>(Resolve(builder.Services));
    }

    // The snapshots it also keeps in memory are a store of their own, built by the container (#498).
    [Fact]
    public void AddDistributedRegistrySnapshots_RegistersTheInMemoryStoreItKeepsSnapshotsIn()
    {
        var builder = CreateBuilder();

        builder.AddDistributedRegistrySnapshots();

        var memory = Assert.Single(builder.Services, d => d.ServiceType == typeof(IRegistrySnapshotStore) && d.IsKeyedService);
        Assert.Equal(DistributedRegistrySnapshotStore.MemoryKey, memory.ServiceKey);
        Assert.Equal(typeof(InMemoryRegistrySnapshotStore), memory.KeyedImplementationType);
    }

    // AddHttp() and AddHttpClients() register the in-memory default with TryAdd: called after this,
    // they keep the distributed store.
    [Fact]
    public void AddDistributedRegistrySnapshots_IsKeptByALaterDefaultRegistration()
    {
        var builder = CreateBuilder();

        builder.AddDistributedRegistrySnapshots();
        builder.Services.TryAddSingleton<IRegistrySnapshotStore, InMemoryRegistrySnapshotStore>();

        Assert.IsType<DistributedRegistrySnapshotStore>(Resolve(builder.Services));
    }

    [Fact]
    public void AddDistributedRegistrySnapshots_ReturnsTheSameBuilder()
    {
        var builder = CreateBuilder();

        Assert.Same(builder, builder.AddDistributedRegistrySnapshots());
    }

    private sealed class FakeKaleidoBuilder(IServiceCollection services) : IKaleidoBuilder
    {
        public IServiceCollection Services { get; } = services;

        public IReadOnlyCollection<Assembly> Assemblies { get; } = [];

        public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();

        public KaleidoServiceOptions ServiceOptions { get; } = new() { ServiceName = "router" };
    }
}
