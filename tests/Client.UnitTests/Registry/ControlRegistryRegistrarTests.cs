using System.Collections.Concurrent;
using Kaleido.Platform.Queryable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Client.Registry.UnitTests;

public sealed class ControlRegistryRegistrarTests : Kaleido.UnitTests.SutFixture
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid InstanceId = Guid.Parse("4b8d3c1e-2f6a-4c7b-9e0d-1a2b3c4d5e6f");
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private enum RegistryOperation { Register, Heartbeat, Deregister }
    private enum RegistryReply { Ok, Unavailable, Forbidden, Unknown }

    private readonly FakeTimeProvider time = new(Start);
    private readonly Mock<IKaleidoRegistryClient> client = new();
    private readonly ConcurrentQueue<(RegistryOperation Operation, RegistryInstanceRegistration? Registration, RegistryHeartbeat? Heartbeat)> calls = new();
    private readonly Mock<ICentralRegistrySource> registry = new();
    private readonly Mock<IKaleidoLocalCapabilities> local = new();
    private Func<RegistryOperation, RegistryReply> answer = _ => RegistryReply.Ok;
    private TaskCompletionSource<int> reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int expected = 1;
    private TimeSpan leaseInterval = Interval;

    public ControlRegistryRegistrarTests()
    {
        local.Setup(x => x.Capabilities()).Returns(new KaleidoRegistry { Queryables = [new QueryableSourceResponse { ServiceName = "code-sets", Name = "CodeSetSource" }] });
        client.Setup(x => x.RegisterAsync(It.IsAny<RegistryInstanceRegistration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegistryInstanceRegistration registration, CancellationToken _) =>
            {
                var status = Record(RegistryOperation.Register, registration: registration);
                return status == RegistryReply.Ok ? Lease() : throw Failure(status);
            });
        client.Setup(x => x.HeartbeatAsync(It.IsAny<Guid>(), It.IsAny<RegistryHeartbeat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, RegistryHeartbeat heartbeat, CancellationToken _) =>
            {
                var status = Record(RegistryOperation.Heartbeat, heartbeat: heartbeat);
                return status == RegistryReply.Unknown ? null : status == RegistryReply.Ok ? Lease() : throw Failure(status);
            });
        client.Setup(x => x.DeregisterAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, CancellationToken _) =>
            {
                Record(RegistryOperation.Deregister);
                return Task.CompletedTask;
            });
    }

    private RegistryLease Lease() => new() { InstanceId = InstanceId, HeartbeatInterval = leaseInterval, LeaseUntilUtc = Start };

    private static KaleidoRegistryClientException Failure(RegistryReply status) =>
        new(RegistryClientErrorCodes.RequestFailed, "The registry refused the call.", status == RegistryReply.Forbidden ? "403" : "503");

    private RegistryReply Record(RegistryOperation operation, RegistryInstanceRegistration? registration = null, RegistryHeartbeat? heartbeat = null)
    {
        calls.Enqueue((operation, registration, heartbeat));
        var status = answer(operation);
        if (calls.Count >= expected)
        {
            reached.TrySetResult(calls.Count);
        }

        return status;
    }

    private ControlRegistryRegistrar CreateSut(HealthCheckService? health = null, double jitter = 0)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IKaleidoRegistryClient))).Returns(client.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new ControlRegistryRegistrar(
            factory.Object,
            new KaleidoServiceOptions { ServiceName = "code-sets", ServiceVersion = "3", InstanceId = InstanceId },
            [local.Object], registry.Object, health is null ? [] : [health], time,
            NullLogger<ControlRegistryRegistrar>.Instance)
        {
            Jitter = () => jitter,
        };
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task CalledAsync(int count)
    {
        expected = count;
        reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (calls.Count < count)
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        }
    }

    private (RegistryOperation Operation, RegistryInstanceRegistration? Registration, RegistryHeartbeat? Heartbeat)[] Calls => [.. calls];

    [Fact]
    public async Task AtStartup_ItRegistersItsOwnCapabilities_ThenReadsTheRegistry()
    {
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        await sut.StopAsync(Cancel);

        var (operation, registration, _) = Calls[0];
        Assert.Equal(RegistryOperation.Register, operation);
        Assert.Equal(("code-sets", "3", InstanceId), (registration?.ServiceName, registration?.ServiceVersion, registration?.InstanceId));
        Assert.Equal("CodeSetSource", Assert.Single(registration!.Queryables).Name);
        Assert.True(sut.HasRegistered);
        registry.Verify(x => x.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARegistrationThatFails_IsRetried_AfterItsBackoff()
    {
        answer = _ => calls.Count == 1 ? RegistryReply.Unavailable : RegistryReply.Ok;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        Assert.False(sut.HasRegistered);
        time.Advance(RegistryRetry.First - TimeSpan.FromTicks(1));
        Assert.Single(Calls);
        time.Advance(TimeSpan.FromTicks(1));
        await CalledAsync(2);
        await sut.StopAsync(Cancel);

        Assert.All(Calls.Take(2), x => Assert.Equal(RegistryOperation.Register, x.Operation));
        Assert.True(sut.HasRegistered);
    }

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

        Assert.Equal(RegistryOperation.Heartbeat, Calls[1].Operation);
        Assert.False(Calls[1].Heartbeat!.Healthy);
    }

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
        Assert.True(Calls[1].Heartbeat!.Healthy);
    }

    [Fact]
    public async Task AHeartbeatTheRegistryDoesNotKnow_RegistersAgain()
    {
        answer = operation => operation == RegistryOperation.Heartbeat ? RegistryReply.Unknown : RegistryReply.Ok;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        time.Advance(Interval);
        await CalledAsync(3);
        await sut.StopAsync(Cancel);

        Assert.Equal([RegistryOperation.Register, RegistryOperation.Heartbeat, RegistryOperation.Register], Calls.Take(3).Select(x => x.Operation));
    }

    [Fact]
    public async Task AHeartbeatThatFails_IsTriedAgainAtTheNextInterval_AndTheInstanceStaysReady()
    {
        answer = operation => operation == RegistryOperation.Heartbeat ? RegistryReply.Unavailable : RegistryReply.Ok;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        time.Advance(Interval);
        await CalledAsync(2);
        time.Advance(Interval);
        await CalledAsync(3);
        await sut.StopAsync(Cancel);

        Assert.Equal([RegistryOperation.Register, RegistryOperation.Heartbeat, RegistryOperation.Heartbeat], Calls.Take(3).Select(x => x.Operation));
        Assert.True(sut.HasRegistered);
    }

    [Fact]
    public async Task AtShutdown_ARegisteredInstance_Deregisters()
    {
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        await sut.StopAsync(Cancel);

        Assert.Equal(RegistryOperation.Deregister, Calls[^1].Operation);
    }

    [Fact]
    public async Task AtShutdown_AnInstanceNeverRegistered_SendsNothing()
    {
        answer = _ => RegistryReply.Forbidden;
        var sut = CreateSut();

        await sut.StartAsync(Cancel);
        await CalledAsync(1);
        await sut.StopAsync(Cancel);

        Assert.DoesNotContain(Calls, x => x.Operation == RegistryOperation.Deregister);
    }

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

        Assert.Equal([RegistryOperation.Register, RegistryOperation.Register, RegistryOperation.Heartbeat], Calls.Take(3).Select(x => x.Operation));
    }
}
