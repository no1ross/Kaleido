using System.Net;
using System.Net.Http.Json;
using Kaleido.Http.Client;
using Kaleido.Http.FunctionalTests.Processor.Behavior;
using Kaleido.Http.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Registry;

// Two real Kaleido hosts: a "behavior" service and a "router" that aggregates it through a real
// Kaleido client. The router's registry must contain what the downstream publishes, with nothing
// reported as failing - the flow consumers (and the MCP adapter) depend on to discover services.
public sealed class RegistryAggregationTests : IAsyncLifetime
{
    private IHost? _downstream;
    private IHost? _router;

    public async ValueTask InitializeAsync()
    {
        _downstream = await StartAsync(
            services => services
                .AddSingleton(new BehaviorSwitches())
                .AddSingleton(new BehaviorGate())
                .AddKaleido(new ConfigurationBuilder().Build(), o =>
                {
                    o.ServiceName = BehaviorFixture.ServiceName;
                    o.Assemblies = [typeof(BehaviorFixture).Assembly];
                    o.TypeFilter = type => type.Namespace == typeof(BehaviorFixture).Namespace;
                })
                .AddHttp(),
            aggregate: false);

        var downstreamHandler = _downstream.GetTestServer().CreateHandler();

        _router = await StartAsync(
            services =>
            {
                services
                    .AddKaleido(
                        new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:Clients:behavior:BaseUrl"] = "http://behavior.test/" })
                            .Build(),
                        o =>
                        {
                            o.ServiceName = "router";
                            o.Assemblies = [typeof(BehaviorFixture).Assembly];
                            o.TypeFilter = _ => false;
                        })
                    .AddHttp()
                    .AddHttpClients();
                services.AddHttpClient(BehaviorFixture.ServiceName).ConfigurePrimaryHttpMessageHandler(() => downstreamHandler);
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
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp(o => o.AggregateRegistry = aggregate));
                });
            })
            .StartAsync();
}
