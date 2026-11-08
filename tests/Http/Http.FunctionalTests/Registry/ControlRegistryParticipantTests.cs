using Kaleido.Http.FunctionalTests.Authorization;
using Kaleido.Http.FunctionalTests.Processor.Behavior;
using Kaleido.Http.Server;
using Kaleido.Platform;
using Kaleido.Platform.Registry;
using Kaleido.Registry;
using Kaleido.Registry.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.FunctionalTests.Registry;

// #553 end to end: a control registry and two Kaleido hosts taking part in it, on one fake clock. Each host
// registers itself with its app-only identity (its app role naming the service; no registrants map), turns
// ready, and serves the whole registry - the other's capabilities with their status - without building
// any of it. An instance going unhealthy, or stopping, shows on the other host within one availability
// check. The registry never calls a participant.
public sealed class ControlRegistryParticipantTests : IAsyncLifetime
{
    private const string ServiceRole = "kaleido-service";
    private const string ReaderRole = "kaleido-reader";
    private const string Consumer = "consumer";

    // One check's worth: the interval (10 s), its jitter (+-10%) and the registry's view of its store (1 s).
    private static readonly TimeSpan WithinOneCheck = TimeSpan.FromSeconds(12);

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly BehaviorSwitches switches = new();
    private bool behaviorHealthy = true;
    private int behaviorRequests;
    private IHost registry = null!;
    private IHost behavior = null!;
    private IHost consumer = null!;
    private RegistryCalls behaviorCalls = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        registry = await StartRegistryAsync();
        behaviorCalls = new RegistryCalls(BehaviorFixture.ServiceName) { InnerHandler = registry.GetTestServer().CreateHandler() };
        behavior = await StartParticipantAsync(BehaviorFixture.ServiceName, behaviorCalls, type => type.Namespace == typeof(BehaviorFixture).Namespace);
        consumer = await StartParticipantAsync(Consumer, new RegistryCalls(Consumer) { InnerHandler = registry.GetTestServer().CreateHandler() }, _ => false);
        await UntilAsync(async () => await ReadyAsync(behavior) && await ReadyAsync(consumer));

        // The registry's view of its store is at most a second old: within one check every host sees both.
        await AdvanceUntilAsync(WithinOneCheck, async () => BehaviorStatus(await ConsumerRegistryAsync()) == RegistryCapabilityStatus.Available);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in new[] { consumer, behavior, registry })
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }
    }

    [Fact]
    public async Task EachHost_RegistersAndTurnsReady_AndServesTheWholeRegistry()
    {
        var served = await ConsumerRegistryAsync();

        var processor = Assert.Single(served.Processes, x => x.ServiceName == BehaviorFixture.ServiceName);
        Assert.Equal(("1", RegistryCapabilityStatus.Available), (processor.ServiceVersion, processor.Status));
        Assert.Contains(processor.InitialSteps, x => x.Name == BehaviorSteps.Start);
        Assert.Empty(served.Errors);
        Assert.Equal(
            [(BehaviorFixture.ServiceName, RegistryCapabilityStatus.Available), (Consumer, RegistryCapabilityStatus.Available)],
            (await RegistryStatusAsync()).Services.Select(x => (x.ServiceName, x.Status)).Order());
        Assert.Equal(0, behaviorRequests);
    }

    // The instance's local check fails: its next heartbeat says so, and the other host shows the version
    // unavailable at its next availability check.
    [Fact]
    public async Task AnInstanceGoingUnhealthy_ShowsOnTheOtherHost_WithinOneCheck()
    {
        behaviorHealthy = false;
        var heartbeat = behaviorCalls.NextHeartbeat();
        await AdvanceUntilAsync(WithinOneCheck, () => Task.FromResult(heartbeat.IsCompleted));

        await AdvanceUntilAsync(WithinOneCheck, async () => BehaviorStatus(await ConsumerRegistryAsync()) == RegistryCapabilityStatus.Unavailable);

        Assert.Equal(RegistryInstanceState.NotReady, Assert.Single(Assert.Single((await RegistryStatusAsync()).Services, x => x.ServiceName == BehaviorFixture.ServiceName).Instances).State);
        Assert.Equal(0, behaviorRequests);
    }

    // Stopping deregisters: the version stays listed, with no instance, and the other host shows it unavailable.
    [Fact]
    public async Task AStoppedInstance_Deregisters_AndShowsUnavailableOnTheOtherHost()
    {
        await behavior.StopAsync(Cancel);

        await AdvanceUntilAsync(WithinOneCheck, async () => BehaviorStatus(await ConsumerRegistryAsync()) == RegistryCapabilityStatus.Unavailable);

        Assert.Empty(Assert.Single((await RegistryStatusAsync()).Services, x => x.ServiceName == BehaviorFixture.ServiceName).Instances);
        Assert.Contains(behaviorCalls.Calls, x => x == HttpMethod.Delete);
    }

    private static RegistryCapabilityStatus? BehaviorStatus(KaleidoRegistry served) =>
        served.Processes.SingleOrDefault(x => x.ServiceName == BehaviorFixture.ServiceName)?.Status;

    private static async Task<bool> ReadyAsync(IHost host) =>
        (await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(x => x.Tags.Contains("ready") && x.Tags.Contains("registry"), Cancel)).Status == HealthStatus.Healthy;

    // Waits for the hosts' background work (registering, a check) to reach a state. Never a time decision:
    // the fake clock makes those, and the test advances it.
    private static async Task<bool> UntilAsync(Func<Task<bool>> condition, TimeSpan? patience = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(patience ?? TimeSpan.FromSeconds(10));

        while (!await condition())
        {
            if (timeout.Token.IsCancellationRequested)
            {
                return patience is not null ? false : throw new TimeoutException("The hosts never reached the state.");
            }

            await Task.Yield();
        }

        return true;
    }

    // Moves the clock a second at a time, at most `limit`, until the condition holds: a timer a host sets a
    // moment after a step still fires at its own time, and the limit is what the test claims.
    private async Task AdvanceUntilAsync(TimeSpan limit, Func<Task<bool>> condition)
    {
        for (var advanced = TimeSpan.Zero; !await UntilAsync(condition, TimeSpan.FromMilliseconds(200)); advanced += TimeSpan.FromSeconds(1))
        {
            Assert.True(advanced < limit, $"Not within {limit} on the hosts' clock.");
            time.Advance(TimeSpan.FromSeconds(1));
        }
    }

    // The consumer's registry as its in-host readers get it (MCP, lookups, hand-offs): it has no capabilities
    // of its own, so it maps no registry endpoint (a user-facing one is PR 2's).
    private async Task<KaleidoRegistry> ConsumerRegistryAsync()
    {
        using var scope = consumer.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IKaleidoRegistryService>().GetForCallerAsync(refresh: false, Cancel);
    }

    private async Task<KaleidoRegistryStatus> RegistryStatusAsync()
    {
        using var client = registry.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/registry/status");
        RegistryCalls.Identify(request, "operations", ReaderRole);
        using var response = await client.SendAsync(request, Cancel);

        return (await response.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, Cancel))!;
    }

    private Task<IHost> StartRegistryAsync() =>
        new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<TimeProvider>(time);
                    services.Configure<MemoryCacheOptions>(options => options.Clock = new RegistryFakeClock(time));
                    services.AddRouting();
                    services.AddAuthentication(TestAuthHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
                    services.AddAuthorization(o =>
                    {
                        o.AddPolicy("registry-reader", p => p.RequireRole(ReaderRole));
                        o.AddPolicy("service-application", p => p.RequireRole(ServiceRole));
                    });
                    services.AddKaleido(
                            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["Kaleido:Registry:ReaderPolicy"] = "registry-reader",
                                ["Kaleido:Registry:RegistrantPolicy"] = "service-application",
                            }).Build(),
                            new()
                            {
                                ServiceName = "registry",
                                AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust,
                                Assemblies = [typeof(ControlRegistryParticipantTests).Assembly],
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
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoControlRegistry());
                });
            })
            .StartAsync(Cancel);

    // A participant: its own capabilities (by namespace), its local health check, and its registry client on
    // the registry's server with its app-only identity.
    private Task<IHost> StartParticipantAsync(string serviceName, RegistryCalls calls, Func<Type, bool> types) =>
        new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<TimeProvider>(time);
                    services.AddRouting();
                    services.AddSingleton(switches).AddSingleton(new BehaviorGate());
                    services.AddHealthChecks().AddCheck("local", () => serviceName != BehaviorFixture.ServiceName || behaviorHealthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
                    services.AddKaleido(
                            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "http://registry.test/" }).Build(),
                            new()
                            {
                                ServiceName = serviceName,
                                ServiceVersion = "1",
                                AuthorizationMode = KaleidoAuthorizationMode.None,
                                Assemblies = [typeof(BehaviorFixture).Assembly],
                                TypeFilter = types,
                            })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddControlRegistryClient();
                    services.AddHttpClient("kaleido-registry").ConfigurePrimaryHttpMessageHandler(() => calls);
                });
                web.Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        if (serviceName == BehaviorFixture.ServiceName)
                        {
                            Interlocked.Increment(ref behaviorRequests);
                        }

                        await next(context);
                    });
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

    // A participant's calls to the registry: sent with its app-only identity (the service role, the reader
    // role and the app role naming its service), recorded, and the next heartbeat awaitable.
    private sealed class RegistryCalls(string serviceName) : DelegatingHandler
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<HttpMethod> _calls = new();
        private TaskCompletionSource _heartbeat = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyCollection<HttpMethod> Calls => [.. _calls];

        // Set before the clock is advanced, so the heartbeat it causes cannot be missed.
        public Task NextHeartbeat()
        {
            _heartbeat = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _heartbeat.Task;
        }

        public static void Identify(HttpRequestMessage request, string subject, string roles)
        {
            request.Headers.Add(TestAuthHandler.UserHeader, subject);
            request.Headers.Add(TestAuthHandler.SubjectHeader, subject);
            request.Headers.Add(TestAuthHandler.RolesHeader, roles);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Identify(request, $"{serviceName}-app", $"{ServiceRole},{ReaderRole},Kaleido.Registrant.{serviceName}");
            var response = await base.SendAsync(request, cancellationToken);
            _calls.Enqueue(request.Method);

            if (request.Method == HttpMethod.Put)
            {
                _heartbeat.TrySetResult();
            }

            return response;
        }
    }
}
