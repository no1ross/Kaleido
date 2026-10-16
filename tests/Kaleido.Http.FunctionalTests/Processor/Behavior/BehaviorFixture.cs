using Kaleido.Http.Client;
using Kaleido.Http.Processor;
using Kaleido.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Behavior;

// A real Kaleido host (in-memory store) with only the Behavior models, and a real typed process
// client wired to it. Framework messages are included so tests can assert the runtime's codes.
public sealed class BehaviorFixture : IAsyncLifetime
{
    public const string ServiceName = "behavior";

    private IHost? _host;
    private ServiceProvider? _clientProvider;

    public HttpClient Client { get; private set; } = null!;
    public IKaleidoProcessorClient ProcessClient { get; private set; } = null!;
    public BehaviorSwitches Switches { get; } = new();
    public BehaviorGate Gate { get; } = new();

    public async ValueTask InitializeAsync()
    {
        _host =
            await new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddSingleton(Switches);
                        services.AddSingleton(Gate);
                        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                            {
                                o.ServiceName = ServiceName;
                                o.Assemblies = [typeof(BehaviorFixture).Assembly];
                                o.TypeFilter = type => type.Namespace == typeof(BehaviorFixture).Namespace;
                            })
                            .AddHttp(o => o.IncludeFrameworkMessages = true);
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                    });
                })
                .StartAsync();

        Client = _host.GetTestClient();

        var handler = _host.GetTestServer().CreateHandler();
        var clientServices = new ServiceCollection();
        clientServices.AddSingleton<IKaleidoCorrelationContextAccessor>(new EmptyCorrelation());
        clientServices.AddKaleido(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { [$"Kaleido:Clients:{ServiceName}:BaseUrl"] = "http://localhost/" })
                    .Build(),
                o =>
                {
                    o.ServiceName = "behavior-caller";
                    o.Assemblies = [typeof(KaleidoServiceOptions).Assembly];
                })
            .AddHttpClients();
        clientServices.AddHttpClient(ServiceName).ConfigurePrimaryHttpMessageHandler(() => handler);
        _clientProvider = clientServices.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        ProcessClient = _clientProvider.CreateScope().ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient(ServiceName);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _clientProvider?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private sealed class EmptyCorrelation : IKaleidoCorrelationContextAccessor
    {
        public KaleidoCorrelationContext Current => new();
    }
}
