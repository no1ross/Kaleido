using Kaleido.Http.Client;
using Kaleido.Http.Process;
using Kaleido.Http.Registry;
using Kaleido.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;

public sealed class ProcessAspNetCoreFixture
    : IAsyncLifetime
{
    private IHost? _host;
    private ServiceProvider? _clientProvider;

    public HttpClient Client { get; private set; } = null!;
    public IKaleidoProcessClientFactory ClientFactory { get; private set; } = null!;
    public TestServer TestServer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
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
                                ["Kaleido:Clients:self:BaseUrl"] = "http://localhost/",
                            })
                            .Build();

                        services.AddKaleido(serverConfig, o =>
                            {
                                o.ServiceName = "kaleido";
                                o.DisplayName = "Test Processor";
                                o.Description = "Test processor.";
                                o.Assemblies = new[] { typeof(ProcessAspNetCoreFixture).Assembly };
                            })
                            .AddHttp()
                            .AddHttpClients();
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapProcessor();
                            endpoints.MapRegistry();
                        });
                    });
                })
                .StartAsync();

        Client = _host.GetTestClient();

        // Wire the Process client factory against the test server.
        TestServer = _host.GetTestServer();
        var testHandler = TestServer.CreateHandler();

        var clientConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Clients:test:BaseUrl"] = "http://localhost/",
                ["Kaleido:Clients:test:RoutePrefix"] = "kaleido",
            })
            .Build();

        var clientServices = new ServiceCollection();
        clientServices.AddSingleton<IKaleidoCorrelationContextAccessor, NullKaleidoCorrelationContextAccessor>();
        clientServices.AddKaleido(clientConfig, o => o.ServiceName = "test-client")
            .AddHttpClients();

        // Override the named HttpClient to use the TestServer handler instead of a real socket
        clientServices.AddHttpClient("test")
            .ConfigurePrimaryHttpMessageHandler(() => testHandler);

        _clientProvider = clientServices.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        ClientFactory = _clientProvider.CreateScope().ServiceProvider.GetRequiredService<IKaleidoProcessClientFactory>();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        _clientProvider?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    // Provides a no-op correlation context for the client factory used in tests
    private sealed class NullKaleidoCorrelationContextAccessor : IKaleidoCorrelationContextAccessor
    {
        public KaleidoCorrelationContext Current => new();
    }
}
