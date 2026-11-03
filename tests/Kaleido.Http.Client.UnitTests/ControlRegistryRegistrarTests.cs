using System.Collections.Concurrent;
using System.Text.Json;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.Client.UnitTests;

// #553: an instance registers at startup, heartbeats at the interval the registry hands out, registers again
// when the registry lost it, and deregisters at shutdown - on the host's clock, with the jitter fixed here.
public sealed class ControlRegistryRegistrarTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid InstanceId = Guid.Parse("4b8d3c1e-2f6a-4c7b-9e0d-1a2b3c4d5e6f");
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider time = new(Start);
    private readonly Mock<HttpMessageHandler> handler = new(MockBehavior.Strict);
    private readonly ConcurrentQueue<(HttpMethod Method, string Path, string? Body)> calls = new();
    private readonly Mock<ICentralRegistrySource> registry = new();
    private readonly Mock<IKaleidoLocalCapabilities> local = new();
    private Func<HttpRequestMessage, HttpStatusCode> answer = _ => HttpStatusCode.OK;
    private TaskCompletionSource<int> reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int expected = 1;
    private TimeSpan leaseInterval = Interval;

    public ControlRegistryRegistrarTests()
    {
        local.Setup(x => x.Capabilities()).Returns(new KaleidoRegistry { Queryables = [new QueryableSourceResponse { ServiceName = "code-sets", Name = "CodeSetSource" }] });
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                calls.Enqueue((request.Method, request.RequestUri!.AbsolutePath, body));
                Assert.Equal(KaleidoCallPurpose.ServiceOwn, Assert.IsType<KaleidoCallContext>(request.Options.TryGetValue(KaleidoCallContext.Key, out var context) ? context : null).Purpose);
                var status = answer(request);
                var response = new HttpResponseMessage(status);

                if (status == HttpStatusCode.OK)
                {
                    response.Content = JsonContent.Create(new RegistryLease { InstanceId = InstanceId, HeartbeatInterval = leaseInterval, LeaseUntilUtc = Start }, options: KaleidoJsonOptions.Options);
                }

                if (calls.Count >= expected)
                {
                    reached.TrySetResult(calls.Count);
                }

                return response;
            });
    }

    private ControlRegistryRegistrar CreateSut(HealthCheckService? health = null, double jitter = 0)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(CentralRegistrySource.ClientName))
            .Returns(() => new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("https://registry.internal/") });

        return new ControlRegistryRegistrar(
            factory.Object,
            new KaleidoRegistryClientOptions { Url = "https://registry.internal", Address = "https://code-sets.internal" },
            new KaleidoServiceOptions { ServiceName = "code-sets", ServiceVersion = "3", InstanceId = InstanceId },
            [local.Object],
            registry.Object,
            health is null ? [] : [health],
            time,
            NullLogger<ControlRegistryRegistrar>.Instance)
        {
            Jitter = () => jitter,
        };
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    // Waits until the registry has been called this many times in all.
    private async Task CalledAsync(int count)
    {
        expected = count;
        reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (calls.Count >= count)
        {
            return;
        }

        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
    }

    private (HttpMethod Method, string Path, string? Body)[] Calls => [.. calls];

    [Fact]
    public async Task AtStartup_ItRegistersItsOwnCapabilities_ThenReadsTheRegistry()
    {
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        await sut.StopAsync(Cancel);

        var (method, path, body) = Calls[0];
        Assert.Equal((HttpMethod.Post, "/registry/instances"), (method, path));
        var registration = JsonSerializer.Deserialize<RegistryInstanceRegistration>(body!, KaleidoJsonOptions.Options)!;
        Assert.Equal(("code-sets", "3", InstanceId, "https://code-sets.internal"), (registration.ServiceName, registration.ServiceVersion, registration.InstanceId, registration.Address));
        Assert.Equal("CodeSetSource", Assert.Single(registration.Queryables).Name);
        Assert.True(sut.HasRegistered);
        registry.Verify(x => x.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // A refused or unreachable registry: retried after 1 s, then 2 s, ... - not before.
    [Fact]
    public async Task ARegistrationThatFails_IsRetried_AfterItsBackoff()
    {
        answer = _ => calls.Count == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        Assert.False(sut.HasRegistered);
        time.Advance(RegistryRetry.First - TimeSpan.FromTicks(1));
        Assert.Single(Calls);
        time.Advance(TimeSpan.FromTicks(1));
        await CalledAsync(2);
        await sut.StopAsync(Cancel);

        Assert.All(Calls.Take(2), x => Assert.Equal(HttpMethod.Post, x.Method));
        Assert.True(sut.HasRegistered);
    }

    // Heartbeats at the interval the lease hands out, with the instance's local health.
    [Fact]
    public async Task OnceRegistered_ItHeartbeatsAtTheLeasesInterval_WithItsLocalHealth()
    {
        var health = new Mock<HealthCheckService>();
        health.Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthReport(new Dictionary<string, HealthReportEntry>(), HealthStatus.Unhealthy, TimeSpan.Zero));
        var sut = CreateSut(health.Object);

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        time.Advance(Interval - TimeSpan.FromTicks(1));
        Assert.Single(Calls);
        time.Advance(TimeSpan.FromTicks(1));
        await CalledAsync(2);
        await sut.StopAsync(Cancel);

        var (method, path, body) = Calls[1];
        Assert.Equal((HttpMethod.Put, $"/registry/instances/{InstanceId}/heartbeat"), (method, path));
        Assert.False(JsonSerializer.Deserialize<RegistryHeartbeat>(body!, KaleidoJsonOptions.Options)!.Healthy);
    }

    // The local checks only: never one calling another service, nor this registration's own.
    [Fact]
    public async Task TheHealthReported_ComesFromLocalChecksOnly()
    {
        Func<HealthCheckRegistration, bool>? predicate = null;
        var health = new Mock<HealthCheckService>();
        health.Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .Callback((Func<HealthCheckRegistration, bool>? p, CancellationToken _) => predicate = p)
            .ReturnsAsync(new HealthReport(new Dictionary<string, HealthReportEntry>(), HealthStatus.Healthy, TimeSpan.Zero));
        var sut = CreateSut(health.Object);

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        time.Advance(Interval);
        await CalledAsync(2);
        await sut.StopAsync(Cancel);

        static HealthCheckRegistration Check(string name, params string[] tags) => new(name, Mock.Of<IHealthCheck>(), null, tags);
        Assert.NotNull(predicate);
        Assert.True(predicate(Check("kaleido-registry-store", "ready")));
        Assert.False(predicate(Check("kaleido-client-member", "remote")));
        Assert.False(predicate(Check(RegistrationHealthCheck.Name, "ready")));
        Assert.True(JsonSerializer.Deserialize<RegistryHeartbeat>(Calls[1].Body!, KaleidoJsonOptions.Options)!.Healthy);
    }

    // The registry lost the instance (it restarted, failed over or removed it): it registers again at once.
    [Fact]
    public async Task AHeartbeatTheRegistryDoesNotKnow_RegistersAgain()
    {
        answer = request => request.Method == HttpMethod.Put ? HttpStatusCode.NotFound : HttpStatusCode.OK;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        time.Advance(Interval);
        await CalledAsync(3);
        await sut.StopAsync(Cancel);

        Assert.Equal([HttpMethod.Post, HttpMethod.Put, HttpMethod.Post], Calls.Take(3).Select(x => x.Method));
    }

    // Any other failure: it goes on, and the instance stays ready.
    [Fact]
    public async Task AHeartbeatThatFails_IsTriedAgainAtTheNextInterval_AndTheInstanceStaysReady()
    {
        answer = request => request.Method == HttpMethod.Put ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        time.Advance(Interval);
        await CalledAsync(2);
        time.Advance(Interval);
        await CalledAsync(3);
        await sut.StopAsync(Cancel);

        Assert.Equal([HttpMethod.Post, HttpMethod.Put, HttpMethod.Put], Calls.Take(3).Select(x => x.Method));
        Assert.True(sut.HasRegistered);
    }

    [Fact]
    public async Task AtShutdown_ARegisteredInstance_Deregisters()
    {
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        await sut.StopAsync(Cancel);

        Assert.Equal((HttpMethod.Delete, $"/registry/instances/{InstanceId}"), (Calls[^1].Method, Calls[^1].Path));
    }

    [Fact]
    public async Task AtShutdown_AnInstanceNeverRegistered_SendsNothing()
    {
        answer = _ => HttpStatusCode.Forbidden;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        await sut.StopAsync(Cancel);

        Assert.DoesNotContain(Calls, x => x.Method == HttpMethod.Delete);
    }

    // The spread: +-10% around a heartbeat's interval, +-20% around a retry.
    [Fact]
    public async Task TheWaits_AreSpreadByTheJitter()
    {
        var sut = CreateSut(jitter: 1);

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        time.Advance((Interval * 1.1) - TimeSpan.FromTicks(1));
        Assert.Single(Calls);
        time.Advance(TimeSpan.FromTicks(1));
        await CalledAsync(2);
        await sut.StopAsync(Cancel);
    }

    // The registry decides the heartbeat's pace: a registration whose lease names no interval does not count,
    // and is retried; once a lease names one, the heartbeats follow it.
    [Fact]
    public async Task ARegistrationWhoseLeaseNamesNoInterval_IsRetried()
    {
        leaseInterval = TimeSpan.Zero;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        Assert.False(sut.HasRegistered);
        leaseInterval = TimeSpan.FromSeconds(20);
        time.Advance(RegistryRetry.First);
        await CalledAsync(2);
        time.Advance(TimeSpan.FromSeconds(20) - TimeSpan.FromTicks(1));
        Assert.Equal(2, Calls.Length);
        time.Advance(TimeSpan.FromTicks(1));
        await CalledAsync(3);
        await sut.StopAsync(Cancel);

        Assert.Equal([HttpMethod.Post, HttpMethod.Post, HttpMethod.Put], Calls.Take(3).Select(x => x.Method));
    }
}
