using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class KaleidoControlRegistryServiceCollectionExtensionsTests
    : SutFixture
{
    private static readonly Dictionary<string, string?> Reader = new() { ["Kaleido:Registry:ReaderPolicy"] = "registry-reader", ["Kaleido:Registry:RegistrantPolicy"] = "service-application" };

    private static IKaleidoBuilder Builder(ServiceCollection services, Dictionary<string, string?>? settings = null, KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.ZeroTrust)
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(Reader).AddInMemoryCollection(settings ?? []).Build());
        builder.Setup(x => x.ServiceOptions).Returns(new KaleidoServiceOptions { ServiceName = "registry", AuthorizationMode = mode });
        return builder.Object;
    }

    [Fact]
    public void AddControlRegistry_BindsKaleidoRegistry_AndRegistersTheRegistry()
    {
        var services = new ServiceCollection();

        Builder(services, new() { ["Kaleido:Registry:HeartbeatInterval"] = "00:00:05", ["Kaleido:Registry:Registrants:radiology:0"] = "radiology-client" }).AddControlRegistry();

        var options = Assert.IsType<KaleidoRegistryOptions>(services.Single(x => x.ServiceType == typeof(KaleidoRegistryOptions)).ImplementationInstance);
        Assert.Equal(TimeSpan.FromSeconds(5), options.HeartbeatInterval);
        Assert.Equal(["radiology-client"], options.Registrants["radiology"]);
        Assert.Contains(services, x => x.ServiceType == typeof(IControlRegistry) && x.ImplementationType == typeof(ControlRegistry));
        Assert.Contains(services, x => x.ServiceType == typeof(IRegistryRegistrants) && x.ImplementationType == typeof(RegistryRegistrants));
        Assert.Contains(services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(RegistryHousekeeping));

        // One full registry per host: the source, the status reader and the health check share it.
        using var provider = services.BuildServiceProvider();
        var source = Assert.IsType<StoreRegistrySource>(provider.GetRequiredService<IKaleidoRegistrySource>());
        Assert.Same(source, provider.GetRequiredService<IKaleidoRegistrySource>());
        Assert.Same(source, provider.GetRequiredService<StoreRegistrySource>());
        var checks = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Contains("ready", checks.Single(x => x.Name == "kaleido-registry-store").Tags);
        Assert.DoesNotContain("ready", checks.Single(x => x.Name == "kaleido-registry-completeness").Tags);
    }

    // AddHttp() registers the "nothing beyond this host" default; the store's registry replaces it.
    [Fact]
    public void AddControlRegistry_ReplacesTheLocalDefault()
    {
        var services = new ServiceCollection();
        services.AddScoped<IKaleidoRegistrySource, LocalRegistrySource>();

        Builder(services).AddControlRegistry();

        var source = Assert.Single(services, x => x.ServiceType == typeof(IKaleidoRegistrySource));
        Assert.Equal((ServiceLifetime.Singleton, null), (source.Lifetime, source.ImplementationType));
    }

    // Only service applications register and only services read: under enforcement both policies are required.
    [Theory]
    [InlineData("ReaderPolicy", KaleidoAuthorizationMode.Authenticated, true)]
    [InlineData("ReaderPolicy", KaleidoAuthorizationMode.ZeroTrust, true)]
    [InlineData("ReaderPolicy", KaleidoAuthorizationMode.None, false)]
    [InlineData("RegistrantPolicy", KaleidoAuthorizationMode.Authenticated, true)]
    [InlineData("RegistrantPolicy", KaleidoAuthorizationMode.ZeroTrust, true)]
    [InlineData("RegistrantPolicy", KaleidoAuthorizationMode.None, false)]
    public void AddControlRegistry_WithoutAPolicy_StopsRegistrationUnderEnforcement(string policy, KaleidoAuthorizationMode mode, bool throws)
    {
        var builder = Builder(new ServiceCollection(), new() { [$"Kaleido:Registry:{policy}"] = " " }, mode);

        var exception = Record.Exception(builder.AddControlRegistry);

        Assert.Equal(throws, exception is KaleidoConfigurationException { Code: ConfigurationErrorCodes.InvalidOptions });
        Assert.Equal(throws, exception is not null);
        Assert.Equal(throws, exception?.Message.Contains(policy, StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AddControlRegistry_AnInvalidSetting_StopsRegistration()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(new ServiceCollection(), new() { ["Kaleido:Registry:MissedHeartbeats"] = "0" }).AddControlRegistry());

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, exception.Code);
    }

    [Fact]
    public void TheStores_AreChosenOnce()
    {
        var services = new ServiceCollection();
        var builder = Builder(services);

        builder.AddInMemoryRegistryStore().AddInMemoryRegistryStore();

        Assert.Equal(typeof(InMemoryKaleidoRegistryStore), services.Single(x => x.ServiceType == typeof(IKaleidoRegistryStore)).ImplementationType);
        Assert.Equal(ConfigurationErrorCodes.ConflictingStore, Assert.Throws<KaleidoConfigurationException>(builder.AddRegistryStore<OwnStore>).Code);
    }

    [Fact]
    public void AddRegistryStore_RegistersTheHostsStore_AsScoped()
    {
        var services = new ServiceCollection();

        Builder(services).AddRegistryStore<OwnStore>().AddRegistryStore<OwnStore>();

        var store = services.Single(x => x.ServiceType == typeof(IKaleidoRegistryStore));
        Assert.Equal((typeof(OwnStore), ServiceLifetime.Scoped), (store.ImplementationType, store.Lifetime));
        Assert.Equal(ConfigurationErrorCodes.ConflictingStore, Assert.Throws<KaleidoConfigurationException>(() => Builder(services).AddInMemoryRegistryStore()).Code);
    }

    [Fact]
    public void TheMethods_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddControlRegistry());
        Assert.Throws<ArgumentNullException>(() => builder!.AddInMemoryRegistryStore());
        Assert.Throws<ArgumentNullException>(() => builder!.AddRegistryStore<OwnStore>());
    }

    private sealed class OwnStore : IKaleidoRegistryStore
    {
        public Task AddRegistrationAsync(RegistryRegistrationRecord registration, string capabilities, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveInstanceAsync(RegistryInstanceRecord instance, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RegistryInstanceChange> RenewInstanceAsync(Guid instanceId, DateTimeOffset heartbeatUtc, bool healthy, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RegistryInstanceChange> RemoveInstanceAsync(Guid instanceId, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, string>> GetCapabilitiesAsync(IReadOnlyCollection<string> revisions, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> RemoveInstancesSeenBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> GetOrSetCurrentVersionAsync(string serviceName, string serviceVersion, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DateTimeOffset> TouchWatchAsync(DateTimeOffset nowUtc, TimeSpan lease, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RegistryStoreContents> ReadAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
