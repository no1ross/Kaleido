using Kaleido.Json;
using Kaleido.Observability;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;

public sealed class QueryableAspNetCoreFixture
    : IAsyncLifetime
{
    private IHost? _host;
    private ServiceProvider? _clientProvider;

    public HttpClient Client { get; private set; } = null!;
    public IKaleidoQueryableClientFactory ClientFactory { get; private set; } = null!;

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

                        services.AddKaleido()
                            .AddAssembly(typeof(FunctionalRecordContext).Assembly)
                            .AddQueryable()
                            .AddQueryableAspNetCore();

                        services.ConfigureHttpJsonOptions(options =>
                        {
                            options.SerializerOptions.Converters.Add(new KaleidoEnumConverterFactory());
                        });
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();

                        app.UseKaleidoExceptionHandling();

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
        var testServer = _host.GetTestServer();
        var testHandler = testServer.CreateHandler();

        var clientServices = new ServiceCollection();
        clientServices.AddSingleton<IKaleidoCorrelationContextAccessor, NullKaleidoCorrelationContextAccessor>();
        clientServices.AddKaleido()
            .AddQueryableClient(o =>
            {
                o.Name = "test";
                o.BaseUrl = "http://localhost/";
                o.RoutePrefix = "kaleido";
            });

        // Override the named HttpClient to use the TestServer handler instead of a real socket
        clientServices.AddHttpClient("test")
            .ConfigurePrimaryHttpMessageHandler(() => testHandler);

        _clientProvider = clientServices.BuildServiceProvider();

        ClientFactory = _clientProvider.GetRequiredService<IKaleidoQueryableClientFactory>();
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
