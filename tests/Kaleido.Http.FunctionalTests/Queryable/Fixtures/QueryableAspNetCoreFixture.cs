using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Http.Queryable;
using Kaleido.Json;
using Kaleido.Observability;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;

public sealed class QueryableAspNetCoreFixture
    : IAsyncLifetime
{
    private IHost? _host;
    private ServiceProvider? _clientProvider;

    public HttpClient Client { get; private set; } = null!;
    public IKaleidoQueryableClientFactory ClientFactory { get; private set; } = null!;
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

                        services.AddSingleton<FunctionalRecordData>();

                        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                            {
                                o.ServiceName = "kaleido";
                                o.Assemblies = new[] { typeof(FunctionalRecordContext).Assembly };
                            })
                            .AddHttp();
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapQueryable();
                        });
                    });
                })
                .StartAsync();

        Client = _host.GetTestClient();

        // Wire the Queryable client factory against the test server.
        // The named HttpClient uses the TestServer's handler so requests stay in-process.
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
        clientServices.AddKaleido(clientConfig, o => o.ServiceName = "test-queryable-client")
            .AddHttpClients();

        // Override the named HttpClient to use the TestServer handler instead of a real socket
        clientServices.AddHttpClient("test")
            .ConfigurePrimaryHttpMessageHandler(() => testHandler);

        _clientProvider = clientServices.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        ClientFactory = _clientProvider.CreateScope().ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>();
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
