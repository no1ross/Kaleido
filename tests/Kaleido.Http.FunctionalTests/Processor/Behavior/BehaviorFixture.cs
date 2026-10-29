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
                        services.AddKaleido(new ConfigurationBuilder().Build(), new()
                        {
                            ServiceName = ServiceName,
                            AuthorizationMode = KaleidoAuthorizationMode.None,
                            Assemblies = [typeof(BehaviorFixture).Assembly],
                            TypeFilter = type => type.Namespace == typeof(BehaviorFixture).Namespace,
                        })
                            .AddInMemoryStores()
                            .AddHttp(new() { IncludeFrameworkMessages = true });
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                    });
                })
                .StartAsync();

        Client = _host.GetTestClient();

        (ProcessClient, _clientProvider) = CreateProcessClient(server => server);
    }

    // A real typed process client (with its resilience pipeline) whose primary handler is the server,
    // wrapped as the test needs. The caller disposes the provider; settings are Kaleido configuration.
    public (IKaleidoProcessorClient Client, ServiceProvider Provider) CreateProcessClient(
        Func<HttpMessageHandler, HttpMessageHandler> wrapServer,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var handler = wrapServer(_host!.GetTestServer().CreateHandler());
        var configuration = new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>(), StringComparer.Ordinal)
        {
            [$"Kaleido:Clients:{ServiceName}:BaseUrl"] = "http://localhost/",
        };

        var clientServices = new ServiceCollection();
        clientServices.AddSingleton<IKaleidoCorrelationContextAccessor>(new EmptyCorrelation());
        clientServices.AddKaleido(
                new ConfigurationBuilder().AddInMemoryCollection(configuration).Build(),
                new()
                {
                    ServiceName = "behavior-caller",
                    AuthorizationMode = KaleidoAuthorizationMode.None,
                    Assemblies = [typeof(KaleidoServiceOptions).Assembly],
                })
            .AddHttpClients();
        clientServices.AddHttpClient(ServiceName).ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = clientServices.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return (provider.CreateScope().ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient(ServiceName), provider);
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
