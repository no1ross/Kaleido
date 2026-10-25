using System.Net;
using Kaleido.Http.Client;
using Kaleido.Http.FunctionalTests.Processor.Behavior;
using Kaleido.Http.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.FunctionalTests.Registry;

// Router replicas over one IDistributedCache (AddDistributedRegistrySnapshots, #275): a downstream
// "behavior" service and router replicas that aggregate it through real Kaleido clients. The downstream
// counts its registry requests, so the tests see how many checks the replicas make together.
public sealed class DistributedRegistrySnapshotTests : IAsyncLifetime
{
    private readonly List<IHost> _hosts = [];
    private readonly SwitchableCache _cache = new();
    private readonly RegistryRequests _downstreamRequests = new();
    private readonly ControlledTime _time = new();
    private TestServer _downstream = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var downstream = await StartAsync(
            services => services
                .AddSingleton(new BehaviorSwitches())
                .AddSingleton(new BehaviorGate())
                .AddKaleido(new ConfigurationBuilder().Build(), o =>
                {
                    o.ServiceName = BehaviorFixture.ServiceName;
                    o.AuthorizationMode = KaleidoAuthorizationMode.None;
                    o.Assemblies = [typeof(BehaviorFixture).Assembly];
                    o.TypeFilter = type => type.Namespace == typeof(BehaviorFixture).Namespace;
                })
                .AddInMemoryStores()
                .AddHttp(),
            map => map.AggregateRegistry = false);

        _downstream = downstream.GetTestServer();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in Enumerable.Reverse(_hosts))
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Fact]
    public async Task Replicas_ShareTheRoutersSnapshot_AndCheckTheDownstreamOnce()
    {
        var replicaA = await StartRouterReplicaAsync();
        var replicaB = await StartRouterReplicaAsync();

        var fromA = await ReadRegistryAsync(replicaA);
        var fromB = await ReadRegistryAsync(replicaB);

        Assert.Equal(1, _downstreamRequests.Count);
        Assert.Equal(fromA.GeneratedAt, fromB.GeneratedAt);
        Assert.Contains(fromB.Processes, x => x.ServiceName == BehaviorFixture.ServiceName);
    }

    // Every request rebuilds the router's snapshot (RegistryCacheTtl zero), so replica B builds its
    // own; the downstream registry it merges is the client copy replica A stored, still fresh.
    [Fact]
    public async Task Replicas_ShareTheClientsCopyOfTheDownstream()
    {
        var replicaA = await StartRouterReplicaAsync(TimeSpan.Zero);
        var replicaB = await StartRouterReplicaAsync(TimeSpan.Zero);

        var fromA = await ReadRegistryAsync(replicaA);
        var fromB = await ReadRegistryAsync(replicaB);

        Assert.NotEqual(fromA.GeneratedAt, fromB.GeneratedAt);
        Assert.Equal(1, _downstreamRequests.Count);
        Assert.Contains(fromB.Processes, x => x.ServiceName == BehaviorFixture.ServiceName);
        Assert.NotNull(await _cache.GetAsync("kaleido-client:router:behavior", Cancel));
    }

    [Fact]
    public async Task AReplica_ServesFromMemoryWhileTheCacheIsDown_AndSharesAgainAfterIt()
    {
        var replicaA = await StartRouterReplicaAsync();
        var replicaB = await StartRouterReplicaAsync();

        _cache.Fail = true;
        var duringOutage = await ReadRegistryAsync(replicaA);
        Assert.Contains(duringOutage.Processes, x => x.ServiceName == BehaviorFixture.ServiceName);
        Assert.Equal(duringOutage.GeneratedAt, (await ReadRegistryAsync(replicaA)).GeneratedAt);

        _cache.Fail = false;
        _time.Advance(TimeSpan.FromSeconds(31));
        var afterOutage = await ReadRegistryAsync(replicaA);
        var fromB = await ReadRegistryAsync(replicaB);

        Assert.NotNull(await _cache.GetAsync("kaleido:router", Cancel));
        Assert.Equal(afterOutage.GeneratedAt, fromB.GeneratedAt);
    }

    private static async Task<AggregatedRegistryResponse> ReadRegistryAsync(IHost router)
    {
        using var client = router.GetTestClient();
        using var response = await client.GetAsync("/router/registry", Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registry = await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options, Cancel);
        Assert.NotNull(registry);
        Assert.False(registry.IsPartial);
        return registry;
    }

    private Task<IHost> StartRouterReplicaAsync(TimeSpan? registryCacheTtl = null) =>
        StartAsync(
            services =>
            {
                services.AddSingleton<IDistributedCache>(_cache);
                services.AddSingleton<TimeProvider>(_time);
                services
                    .AddKaleido(
                        new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:Clients:behavior:BaseUrl"] = "http://behavior.test/" })
                            .Build(),
                        o =>
                        {
                            o.ServiceName = "router";
                            o.AuthorizationMode = KaleidoAuthorizationMode.None;
                            o.Assemblies = [typeof(BehaviorFixture).Assembly];
                            o.TypeFilter = _ => false;
                        })
                    .AddInMemoryStores()
                    .AddHttp()
                    .AddHttpClients()
                    .AddDistributedRegistrySnapshots();
                services
                    .AddHttpClient(BehaviorFixture.ServiceName)
                    .ConfigurePrimaryHttpMessageHandler(() => new CountingHandler(_downstreamRequests) { InnerHandler = _downstream.CreateHandler() });
            },
            map =>
            {
                map.AggregateRegistry = true;
                if (registryCacheTtl is { } ttl)
                {
                    map.RegistryCacheTtl = ttl;
                }
            });

    private async Task<IHost> StartAsync(Action<IServiceCollection> configure, Action<KaleidoHttpMapOptions> map)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    configure(services);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp(map));
                });
            })
            .StartAsync(Cancel);

        _hosts.Add(host);
        return host;
    }

    private sealed class RegistryRequests
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Add() => Interlocked.Increment(ref _count);
    }

    private sealed class CountingHandler(RegistryRequests requests) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal) == true)
            {
                requests.Add();
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    // A real in-process IDistributedCache that can be switched off, as an unreachable Redis would be.
    private sealed class SwitchableCache : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public bool Fail { get; set; }

        public byte[]? Get(string key) => Guard(() => _inner.Get(key));

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Guard(() => _inner.GetAsync(key, token));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Guard(() => { _inner.Set(key, value, options); return 0; });

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Guard(() => _inner.SetAsync(key, value, options, token));

        public void Refresh(string key) => Guard(() => { _inner.Refresh(key); return 0; });

        public Task RefreshAsync(string key, CancellationToken token = default) => Guard(() => _inner.RefreshAsync(key, token));

        public void Remove(string key) => Guard(() => { _inner.Remove(key); return 0; });

        public Task RemoveAsync(string key, CancellationToken token = default) => Guard(() => _inner.RemoveAsync(key, token));

        private T Guard<T>(Func<T> call) => Fail ? throw new TimeoutException("The cache is unreachable.") : call();
    }

    private sealed class ControlledTime : TimeProvider
    {
        private long _offsetTicks;

        public override DateTimeOffset GetUtcNow() => base.GetUtcNow().AddTicks(Interlocked.Read(ref _offsetTicks));

        public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);
    }
}
