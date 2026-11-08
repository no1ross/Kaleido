using System.Collections.Concurrent;
using Kaleido.Http.Server;
using Kaleido.Platform;
using Kaleido.Platform.Registry;
using Kaleido.Registry;
using Kaleido.Registry.Http;
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
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> registered = new();
    private IHost host = null!;

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
                    services.AddSingleton<TimeProvider>(registry.time);
                    services.Configure<MemoryCacheOptions>(options => options.Clock = new RegistryFakeClock(registry.time));
                    services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = "registry",
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(TestControlRegistry).Assembly],
                        TypeFilter = _ => false,
                    })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddControlRegistry()
                        .AddInMemoryRegistryStore();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoControlRegistry());
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);
        return registry;
    }

    public HttpMessageHandler CreateHandler() => new RegistrationHandler(this) { InnerHandler = host.GetTestServer().CreateHandler() };

    public HttpClient CreateClient() => host.GetTestClient();

    public async Task WaitForRegistrationAsync(IHost participant)
    {
        var instanceId = participant.Services.GetRequiredService<KaleidoServiceOptions>().InstanceId;
        await registered.GetOrAdd(instanceId, _ => new(TaskCreationOptions.RunContinuationsAsynchronously))
            .Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
        using var scope = participant.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IKaleidoRegistrySource>().RefreshAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync(CancellationToken.None);
        host.Dispose();
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
                    registry.registered.GetOrAdd(instance.InstanceId, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
                }
            }

            return response;
        }
    }
}
