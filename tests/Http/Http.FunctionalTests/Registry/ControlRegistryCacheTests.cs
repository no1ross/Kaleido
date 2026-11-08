using System.Net;
using Kaleido.Http.Server;
using Kaleido.Platform;
using Kaleido.Platform.Queryable;
using Kaleido.Platform.Registry;
using Kaleido.Registry;
using Kaleido.Registry.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.FunctionalTests.Registry;

public sealed class ControlRegistryCacheTests : IAsyncLifetime
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryKaleidoRegistryStore sharedStore = new(NullLogger<InMemoryKaleidoRegistryStore>.Instance);
    private readonly List<IHost> hosts = [];
    private SharedDistributedCache? distributed;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var host in hosts)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }

    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public async Task WithoutADistributedCache_TheViewIsSharedUntilItsConfiguredLifetime(long ticksFromExpiration, bool sameView)
    {
        var (host, _) = await StartAsync(viewTtl: TimeSpan.FromSeconds(3));
        await RegisterAsync(host);
        using var client = host.GetTestClient();

        var first = await ReadAsync<KaleidoRegistry>(client, "/registry");
        time.Advance(TimeSpan.FromSeconds(3) + TimeSpan.FromTicks(ticksFromExpiration));
        var second = await ReadAsync<KaleidoRegistryStatus>(client, "/registry/status");

        Assert.Equal("PatientSource", Assert.Single(first.Queryables).Name);
        Assert.Equal(sameView ? first.GeneratedAt : time.GetUtcNow(), second.GeneratedAt);
    }

    [Fact]
    public async Task AfterAViewExpires_AnUnchangedCapabilityRevisionDoesNotNeedTheStore()
    {
        var (host, store) = await StartAsync(viewTtl: TimeSpan.FromSeconds(2));
        await RegisterAsync(host);
        using var client = host.GetTestClient();
        var first = await ReadAsync<KaleidoRegistry>(client, "/registry");

        store.FailCapabilityReads = true;
        time.Advance(TimeSpan.FromSeconds(2));
        var next = await ReadAsync<KaleidoRegistry>(client, "/registry");

        Assert.Equal(first.Revision, next.Revision);
        Assert.Equal("PatientSource", Assert.Single(next.Queryables).Name);
        Assert.NotEqual(first.GeneratedAt, next.GeneratedAt);
    }

    [Fact]
    public async Task ACapabilityRevision_IsRefetchedOnlyAfterItsConfiguredLifetime()
    {
        var (host, store) = await StartAsync(viewTtl: TimeSpan.FromSeconds(1), capabilitiesTtl: TimeSpan.FromSeconds(4));
        await RegisterAsync(host);
        using var client = host.GetTestClient();
        var first = await ReadAsync<KaleidoRegistry>(client, "/registry");

        store.FailCapabilityReads = true;
        time.Advance(TimeSpan.FromSeconds(4) - TimeSpan.FromTicks(1));
        var beforeExpiry = await ReadAsync<KaleidoRegistry>(client, "/registry");
        time.Advance(TimeSpan.FromTicks(1));
        await host.Services.GetRequiredService<IKaleidoRegistrySource>().RefreshAsync(Cancel);
        using var expired = await client.GetAsync("/registry", Cancel);
        store.FailCapabilityReads = false;
        var recovered = await ReadAsync<KaleidoRegistry>(client, "/registry");

        Assert.Equal(first.Revision, beforeExpiry.Revision);
        Assert.Equal(HttpStatusCode.InternalServerError, expired.StatusCode);
        Assert.Equal(first.Revision, recovered.Revision);
    }

    [Fact]
    public async Task TwoRegistryNodes_ShareTheBuiltViewThroughTheDistributedCache()
    {
        distributed = new SharedDistributedCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var (first, _) = await StartAsync(viewTtl: TimeSpan.FromMinutes(1), distributed);
        var (second, secondStore) = await StartAsync(viewTtl: TimeSpan.FromMinutes(1), distributed);
        await RegisterAsync(first);
        using var firstClient = first.GetTestClient();
        using var secondClient = second.GetTestClient();

        var source = await ReadAsync<KaleidoRegistry>(firstClient, "/registry");
        secondStore.FailReads = true;
        var fromSecondNode = await ReadAsync<KaleidoRegistry>(secondClient, "/registry");

        Assert.Equal("PatientSource", Assert.Single(fromSecondNode.Queryables).Name);
        Assert.Equal(source.Revision, fromSecondNode.Revision);
        Assert.Equal(source.GeneratedAt, fromSecondNode.GeneratedAt);
    }

    [Fact]
    public async Task AnUnreachableDistributedCache_DoesNotPreventARegistryReadOrRefresh()
    {
        var (host, _) = await StartAsync(viewTtl: TimeSpan.FromSeconds(2), new UnavailableDistributedCache());
        await RegisterAsync(host);
        using var client = host.GetTestClient();

        var first = await ReadAsync<KaleidoRegistry>(client, "/registry");
        time.Advance(TimeSpan.FromTicks(1));
        await host.Services.GetRequiredService<IKaleidoRegistrySource>().RefreshAsync(Cancel);
        var refreshed = await ReadAsync<KaleidoRegistry>(client, "/registry");

        Assert.Equal("PatientSource", Assert.Single(refreshed.Queryables).Name);
        Assert.Equal(first.Revision, refreshed.Revision);
        Assert.Equal(time.GetUtcNow(), refreshed.GeneratedAt);
    }

    private async Task<(IHost Host, ReadFaultRegistryStore Store)> StartAsync(TimeSpan viewTtl, IDistributedCache? distributedCache = null, TimeSpan? capabilitiesTtl = null)
    {
        var store = new ReadFaultRegistryStore(sharedStore);
        var settings = new Dictionary<string, string?>
        {
            ["Kaleido:Registry:Cache:ViewTtl"] = viewTtl.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
        };
        if (capabilitiesTtl is { } ttl)
        {
            settings["Kaleido:Registry:Cache:CapabilitiesTtl"] = ttl.ToString("c", System.Globalization.CultureInfo.InvariantCulture);
        }

        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<TimeProvider>(time);
                    services.Configure<MemoryCacheOptions>(options => options.Clock = new RegistryFakeClock(time));
                    services.AddSingleton<IKaleidoRegistryStore>(store);
                    if (distributedCache is not null)
                    {
                        services.AddSingleton(distributedCache);
                    }

                    services.AddRouting();
                    services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new()
                    {
                        ServiceName = "registry",
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(ControlRegistryCacheTests).Assembly],
                        TypeFilter = _ => false,
                    })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddControlRegistry();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoControlRegistry());
                });
            })
            .StartAsync(Cancel);
        hosts.Add(host);
        return (host, store);
    }

    private static async Task RegisterAsync(IHost host)
    {
        using var client = host.GetTestClient();
        using var response = await client.PostAsJsonAsync("/registry/instances", new RegistryInstanceRegistration
        {
            ServiceName = "clinic",
            ServiceVersion = "1",
            InstanceId = Guid.NewGuid(),
            Queryables = [new QueryableSourceResponse { ServiceName = "clinic", Name = "PatientSource", QueryUrl = "/clinic/queryable/patientsource/query" }],
        }, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Cancel);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(KaleidoJsonOptions.Options, Cancel))!;
    }

    private sealed class ReadFaultRegistryStore(IKaleidoRegistryStore inner) : IKaleidoRegistryStore
    {
        public bool FailReads { get; set; }
        public bool FailCapabilityReads { get; set; }

        public Task AddRegistrationAsync(RegistryRegistrationRecord registration, string capabilities, CancellationToken cancellationToken) => inner.AddRegistrationAsync(registration, capabilities, cancellationToken);
        public Task SaveInstanceAsync(RegistryInstanceRecord instance, CancellationToken cancellationToken) => inner.SaveInstanceAsync(instance, cancellationToken);
        public Task<RegistryInstanceChange> RenewInstanceAsync(Guid instanceId, DateTimeOffset heartbeatUtc, bool healthy, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken) => inner.RenewInstanceAsync(instanceId, heartbeatUtc, healthy, allowedServices, cancellationToken);
        public Task<RegistryInstanceChange> RemoveInstanceAsync(Guid instanceId, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken) => inner.RemoveInstanceAsync(instanceId, allowedServices, cancellationToken);
        public Task<int> RemoveInstancesSeenBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) => inner.RemoveInstancesSeenBeforeAsync(cutoffUtc, cancellationToken);
        public Task<string> GetOrSetCurrentVersionAsync(string serviceName, string serviceVersion, CancellationToken cancellationToken) => inner.GetOrSetCurrentVersionAsync(serviceName, serviceVersion, cancellationToken);
        public Task<DateTimeOffset> TouchWatchAsync(DateTimeOffset nowUtc, TimeSpan lease, CancellationToken cancellationToken) => inner.TouchWatchAsync(nowUtc, lease, cancellationToken);
        public Task<RegistryStoreContents> ReadAsync(CancellationToken cancellationToken) => FailReads ? throw new IOException("Store read unavailable") : inner.ReadAsync(cancellationToken);
        public Task<IReadOnlyDictionary<string, string>> GetCapabilitiesAsync(IReadOnlyCollection<string> revisions, CancellationToken cancellationToken) => FailCapabilityReads ? throw new IOException("Capability read unavailable") : inner.GetCapabilitiesAsync(revisions, cancellationToken);
    }

    private sealed class SharedDistributedCache(MemoryDistributedCache inner) : IDistributedCache
    {
        public byte[]? Get(string key) => inner.Get(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key) => inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => inner.SetAsync(key, value, options, token);
    }

    private sealed class UnavailableDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new IOException("Distributed cache unavailable");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromException<byte[]?>(new IOException("Distributed cache unavailable"));
        public void Refresh(string key) => throw new IOException("Distributed cache unavailable");
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.FromException(new IOException("Distributed cache unavailable"));
        public void Remove(string key) => throw new IOException("Distributed cache unavailable");
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.FromException(new IOException("Distributed cache unavailable"));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new IOException("Distributed cache unavailable");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.FromException(new IOException("Distributed cache unavailable"));
    }
}
