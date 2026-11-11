using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Server.UnitTests;

public sealed class ProcessServerServiceCollectionExtensionsTests : SutFixture
{
    [Fact]
    public void AddIdempotencyStore_RegistersScopedAndSameStoreTwiceIsIdempotent()
    {
        var builder = CreateBuilder();
        builder.AddIdempotencyStore<OwnStore>().AddIdempotencyStore<OwnStore>();

        var descriptor = Assert.Single(builder.Services, x => x.ServiceType == typeof(IProcessIdempotencyStore));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(OwnStore), descriptor.ImplementationType);
    }

    [Fact]
    public void AddIdempotencyStore_RejectsDifferentRegisteredTypesAndFactories()
    {
        var builder = CreateBuilder();
        builder.AddIdempotencyStore<OwnStore>();
        var conflict = Assert.Throws<KaleidoConfigurationException>(builder.AddIdempotencyStore<OtherStore>);
        Assert.Equal(ConfigurationErrorCodes.ConflictingStore, conflict.Code);
        Assert.Equal(typeof(OwnStore), Assert.Single(builder.Services, x => x.ServiceType == typeof(IProcessIdempotencyStore)).ImplementationType);

        var factoryBuilder = CreateBuilder();
        factoryBuilder.Services.AddScoped(_ => Mock.Of<IProcessIdempotencyStore>());
        Assert.Equal(ConfigurationErrorCodes.ConflictingStore,
            Assert.Throws<KaleidoConfigurationException>(factoryBuilder.AddIdempotencyStore<OwnStore>).Code);

        var instanceBuilder = CreateBuilder();
        instanceBuilder.Services.AddSingleton(Mock.Of<IProcessIdempotencyStore>());
        Assert.Equal(ConfigurationErrorCodes.ConflictingStore,
            Assert.Throws<KaleidoConfigurationException>(instanceBuilder.AddIdempotencyStore<OwnStore>).Code);
    }

    [Fact]
    public void AddIdempotencyStore_RejectsNullBuilder()
    {
        IKaleidoBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddIdempotencyStore<OwnStore>());
    }

    private static IKaleidoBuilder CreateBuilder() =>
        new ServiceCollection().AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", ServiceVersion = "1", Assemblies = [typeof(ProcessServerServiceCollectionExtensionsTests).Assembly] });

    private sealed class OwnStore : IProcessIdempotencyStore
    {
        public Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OtherStore : IProcessIdempotencyStore
    {
        public Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
