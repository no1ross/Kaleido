using Kaleido.Json;
using Kaleido.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
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

                        services.AddKaleido()
                            .AddAssembly(typeof(ProcessAspNetCoreFixture).Assembly)
                            .AddProcessor(o =>
                            {
                                o.Name = "test-processor";
                                o.Description = "Test processor.";
                                o.Version = "1.0.0";
                                o.DisplayName = "Test Processor";
                            })
                            .AddProcessorAspNetCore();

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
                            endpoints.MapProcessor();
                        });
                    });
                })
                .StartAsync();

        Client = _host.GetTestClient();

        // Wire the Process client factory against the test server.
        TestServer = _host.GetTestServer();
        var testHandler = TestServer.CreateHandler();

        var clientServices = new ServiceCollection();
        clientServices.AddSingleton<IKaleidoCorrelationContextAccessor, NullKaleidoCorrelationContextAccessor>();
        clientServices.AddKaleido()
            .AddProcessClient(o =>
            {
                o.Name = "test";
                o.BaseUrl = "http://localhost/";
                o.RoutePrefix = "kaleido";
            });

        // Override the named HttpClient to use the TestServer handler instead of a real socket
        clientServices.AddHttpClient("test")
            .ConfigurePrimaryHttpMessageHandler(() => testHandler);

        _clientProvider = clientServices.BuildServiceProvider();

        ClientFactory = _clientProvider.GetRequiredService<IKaleidoProcessClientFactory>();
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
