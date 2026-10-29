using System.Net;
using Kaleido.Http.Client;
using Kaleido.Http.FunctionalTests.Processor.Behavior;
using Kaleido.Http.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.FunctionalTests.Registry;

// Two real Kaleido hosts: a "behavior" service and a "router" that aggregates it through a real
// Kaleido client. The router's registry must contain what the downstream publishes, with nothing
// reported as failing - the flow consumers (and the MCP adapter) depend on to discover services.
public sealed class RegistryAggregationTests : IAsyncLifetime
{
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromSeconds(30);

    // The router's clock (#460), so the refresh cooldown is crossed without waiting. The snapshot
    // never expires (RegistryCacheTtl = null), so only a refresh rebuilds it.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly RegistryRequestCounter _downstreamRegistryReads = new();
    private IHost? _downstream;
    private IHost? _router;

    public async ValueTask InitializeAsync()
    {
        _downstream = await StartAsync(
            services => services
                .AddSingleton(new BehaviorSwitches())
                .AddSingleton(new BehaviorGate())
                .AddKaleido(new ConfigurationBuilder().Build(), new()
                {
                    ServiceName = BehaviorFixture.ServiceName,
                    AuthorizationMode = KaleidoAuthorizationMode.None,
                    Assemblies = [typeof(BehaviorFixture).Assembly],
                    TypeFilter = type => type.Namespace == typeof(BehaviorFixture).Namespace,
                })
                .AddInMemoryStores()
                .AddHttp(),
            aggregate: false);

        _downstreamRegistryReads.InnerHandler = _downstream.GetTestServer().CreateHandler();

        _router = await StartAsync(
            services =>
            {
                services.AddSingleton<TimeProvider>(_time);
                services
                    .AddKaleido(
                        new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:Clients:behavior:BaseUrl"] = "http://behavior.test/" })
                            .Build(),
                        new()
                        {
                            ServiceName = "router",
                            AuthorizationMode = KaleidoAuthorizationMode.None,
                            Assemblies = [typeof(BehaviorFixture).Assembly],
                            TypeFilter = _ => false,
                        })
                    .AddInMemoryStores()
                    .AddHttp()
                    .AddHttpClients();
                services.AddHttpClient(BehaviorFixture.ServiceName).ConfigurePrimaryHttpMessageHandler(() => _downstreamRegistryReads);
            },
            aggregate: true);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in new[] { _router, _downstream })
        {
            if (host is not null)
            {
                await host.StopAsync();
                host.Dispose();
            }
        }
    }

    [Fact]
    public async Task TheRoutersRegistry_ContainsTheDownstreamsCapabilities_AndNoErrors()
    {
        using var client = _router!.GetTestClient();

        using var response = await client.GetAsync("/router/registry", TestContext.Current.CancellationToken);
        var registry = await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(registry);
        Assert.False(registry.IsPartial);
        Assert.Empty(registry.ClientErrors);

        var processor = Assert.Single(registry.Processes, x => x.ServiceName == BehaviorFixture.ServiceName);
        Assert.Contains(processor.InitialSteps, x => x.Name == BehaviorSteps.Start);
        var close = Assert.Single(processor.Steps!, x => x.Name == BehaviorSteps.Close);
        Assert.Equal("/behavior/processes/steps/behaviorclosestep", close.ExecuteUrl);
        Assert.Contains(close.Dependencies, x => x.Name == BehaviorSteps.Start);
    }

    [Fact]
    public async Task AStrictRegistryRead_OfAHealthyAggregate_Succeeds()
    {
        using var client = _router!.GetTestClient();

        using var response = await client.GetAsync("/router/registry?strict", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // #503: ?refresh on an aggregated registry rebuilds it from the downstreams, but not more often
    // than the cooldown: a refresh inside it is answered from the snapshot, one at its end reads the
    // downstream again. Catches ?refresh ignored on a router, or the cooldown not throttling it.
    [Fact]
    public async Task ARefresh_RereadsTheDownstreams_ButNotInsideTheCooldown()
    {
        using var client = _router!.GetTestClient();
        var cancel = TestContext.Current.CancellationToken;

        using (await client.GetAsync("/router/registry", cancel))
        {
        }

        var afterFirstRead = _downstreamRegistryReads.Count;

        using (await client.GetAsync("/router/registry?refresh", cancel))
        {
        }

        Assert.Equal(afterFirstRead, _downstreamRegistryReads.Count);

        _time.Advance(RefreshCooldown);
        using var refreshed = await client.GetAsync("/router/registry?refresh", cancel);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(afterFirstRead + 1, _downstreamRegistryReads.Count);
    }

    // The router's calls to the downstream: counts its registry reads.
    private sealed class RegistryRequestCounter : DelegatingHandler
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal) == true)
            {
                Interlocked.Increment(ref _count);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    private static Task<IHost> StartAsync(Action<IServiceCollection> configure, bool aggregate) =>
        new HostBuilder()
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
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp(new() { AggregateRegistry = aggregate, RegistryCacheTtl = null, RegistryRefreshCooldown = RefreshCooldown }));
                });
            })
            .StartAsync();
}
