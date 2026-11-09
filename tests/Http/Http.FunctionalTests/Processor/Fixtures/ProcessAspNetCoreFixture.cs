using Kaleido.Client.Processor;
using Kaleido.Http.Server;
using Kaleido.Observability;
using Kaleido.Platform.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Fixtures;

public sealed class ProcessorAspNetCoreFixture
    : IAsyncLifetime
{
    private IHost? _host;
    private TestControlRegistry? _registry;
    private ServiceProvider? _clientProvider;

    public HttpClient Client { get; private set; } = null!;
    public HttpClient RegistryClient { get; private set; } = null!;
    public IKaleidoProcessorClientFactory ClientFactory { get; private set; } = null!;
    public TestServer TestServer { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _registry = await TestControlRegistry.StartAsync();
        RegistryClient = _registry.CreateClient();
        _host =
            await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseTestServer();

                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddRouting();

                        var serverConfig = new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["Kaleido:BaseUrl"] = "http://registry.test/",
                            })
                            .Build();

                        services.AddKaleido(serverConfig, new()
                        {
                            ServiceName = "kaleido",
                            ServiceVersion = "1",
                            AuthorizationMode = KaleidoAuthorizationMode.None,
                            DisplayName = "Test Processor",
                            Description = "Test processor.",
                            Assemblies = new[] { typeof(ProcessorAspNetCoreFixture).Assembly },
                        })
                            .AddInMemoryStores()
                            .AddHttp()
                            .AddHttpClients()
                            .AddControlRegistryClient();
                        services.AddHttpClient("kaleido-registry").ConfigurePrimaryHttpMessageHandler(() => _registry!.CreateHandler());
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapKaleidoHttp();
                        });
                    });
                })
                .StartAsync();

        await _registry.WaitForRegistrationAsync(_host);
        Client = _host.GetTestClient();

        // Wire the Process client factory against the test server.
        TestServer = _host.GetTestServer();
        var testHandler = TestServer.CreateHandler();

        var clientConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:BaseUrl"] = "http://localhost/",
            })
            .Build();

        var clientServices = new ServiceCollection();
        clientServices.AddSingleton<IKaleidoRegistrySource>(_host.Services.GetRequiredService<IKaleidoRegistrySource>());
        clientServices.AddSingleton<IKaleidoCorrelationContextAccessor, NullKaleidoCorrelationContextAccessor>();
        // Client container needs no type scanning; under xUnit v3 the entry assembly
        // is this test project (it was testhost under v2), so the fallback would
        // register functional test sources without their dependencies.
        clientServices.AddKaleido(clientConfig, new()
        {
            ServiceName = "test-client",
            AuthorizationMode = KaleidoAuthorizationMode.None,
            Assemblies = [typeof(KaleidoServiceOptions).Assembly],
        })
            .AddHttpClients();

        // Override the named HttpClient to use the TestServer handler instead of a real socket
        clientServices.AddHttpClient(KaleidoClientTransport.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => testHandler);

        _clientProvider = clientServices.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        ClientFactory = _clientProvider.CreateScope().ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        RegistryClient.Dispose();
        _clientProvider?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_registry is not null)
        {
            await _registry.DisposeAsync();
        }
    }

    // Provides a no-op correlation context for the client factory used in tests
    private sealed class NullKaleidoCorrelationContextAccessor : IKaleidoCorrelationContextAccessor
    {
        public KaleidoCorrelationContext Current => new();
    }
}
