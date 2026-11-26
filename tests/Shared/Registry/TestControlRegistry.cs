using System.Collections.Concurrent;
using Kaleido.Hosting.Http;
using Kaleido.Registry.Http.Server;
using Kaleido.Registry.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Testing.Registry;

public sealed class TestControlRegistry : IAsyncDisposable
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly ConcurrentDictionary<(string Name, string Version), TaskCompletionSource> registered = new();
    private IHost? host;

    private IHost Host => host ?? throw new InvalidOperationException("The Registry authority has not started.");

    private TestControlRegistry()
    {
    }

    public static async Task<TestControlRegistry> StartAsync()
    {
        var registry = new TestControlRegistry();
        registry.host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddTestJwtBearer();
                    services.AddAuthorizationBuilder()
                        .AddPolicy("registry-registrant", policy => policy.RequireRole("registrant"))
                        .AddPolicy("registry-reader", policy => policy.RequireRole("registry-reader"));
                    services.AddSingleton<TimeProvider>(registry.time);
                    services.Configure<MemoryCacheOptions>(options => options.Clock = new RegistryFakeClock(registry.time));
                    services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = "registry",
                        ServiceVersion = "1",
                        Assemblies = [typeof(TestControlRegistry).Assembly],
                        TypeFilter = _ => false,
                    })
                        .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
                        .AddHttp()
                        .AddControlRegistry(new KaleidoRegistryOptions { RegistrantPolicy = "registry-registrant", ReaderPolicy = "registry-reader" })
                        .AddInMemoryRegistryStore()
                        .AddRegistryHttpServer();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapKaleido().MapCentralRegistry());
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);
        return registry;
    }

    public HttpMessageHandler CreateHandler() => new RegistrationHandler(this) { InnerHandler = Host.GetTestServer().CreateHandler() };

    public HttpClient CreateClient() => Host.GetTestClient();

    public async Task WaitForRegistrationAsync(IHost participant)
    {
        var service = participant.Services.GetRequiredService<KaleidoServiceOptions>();
        await registered.GetOrAdd((service.ServiceName, service.ServiceVersion), _ => new(TaskCreationOptions.RunContinuationsAsynchronously))
            .Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
        using var registryScope = Host.Services.CreateScope();
        using var participantScope = participant.Services.CreateScope();
        var registry = registryScope.ServiceProvider.GetRequiredService<IKaleidoRegistrySource>();
        var hydrated = participantScope.ServiceProvider.GetRequiredService<IKaleidoRegistrySource>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await registry.RefreshAsync(TestContext.Current.CancellationToken);
            await hydrated.RefreshAsync(TestContext.Current.CancellationToken);
            var view = await hydrated.GetAsync(TestContext.Current.CancellationToken);
            if (view.Routes.Any(route => route.ServiceName == service.ServiceName && route.ServiceVersion == service.ServiceVersion))
            {
                return;
            }

            time.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
        }

        throw new InvalidOperationException($"The central registry did not publish {service.ServiceName}/{service.ServiceVersion} after registration.");
    }

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }
    }

    private sealed class RegistrationHandler(TestControlRegistry registry) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/registry/instances" && response.IsSuccessStatusCode && request.Content is not null)
            {
                var instance = await request.Content.ReadFromJsonAsync<RegistryInstanceRegistration>(KaleidoJsonOptions.Options, cancellationToken);
                if (instance is not null)
                {
                    registry.registered.GetOrAdd((instance.ServiceName, instance.ServiceVersion), _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
                }
            }

            return response;
        }
    }
}
