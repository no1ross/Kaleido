using Kaleido.Queryable.FunctionalTests.Infrastructure;
using Kaleido.Queryable.Shared;
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
                            .AddAssembly(typeof(SampleKaleidoRecord).Assembly)
                            .AddAssembly(typeof(SampleKaleidoRecordSource).Assembly)
                            .AddQueryable()
                            .AddQueryableAspNetCore();

                        services.AddSingleton<SampleKaleidoCsvData>();
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