using Kaleido.Json;
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

    public HttpClient Client { get; private set; } = null!;

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
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
