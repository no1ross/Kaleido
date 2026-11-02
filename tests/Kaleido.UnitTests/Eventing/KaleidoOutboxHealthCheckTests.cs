using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Eventing.UnitTests;

// #552: every service's outbox health check - a relay seen recently, and no backlog - from the outbox's
// own database only.
public sealed class KaleidoOutboxHealthCheckTests
    : SutFixture
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<IKaleidoOutboxStore> store = new();

    private static readonly KaleidoEventOptions Defaults = new();

    private KaleidoOutboxHealthCheck CreateSut(KaleidoEventOptions? events = null) =>
        new(store.Object, new KaleidoServiceOptions { ServiceName = "orders", Events = events ?? Defaults }, time);

    private DateTimeOffset Now => time.GetUtcNow();

    private void Status(DateTimeOffset? lastRelaySeen, long dueCount = 0, DateTimeOffset? oldestDue = null) =>
        store.Setup(x => x.GetStatusAsync(Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KaleidoOutboxStatus { LastRelaySeenUtc = lastRelaySeen, DueCount = dueCount, OldestDueUtc = oldestDue });

    private Task<HealthCheckResult> CheckAsync(KaleidoEventOptions? events = null) =>
        CreateSut(events).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task ARelaySeenNow_AndNoBacklog_IsHealthy()
    {
        Status(Now);

        Assert.Equal(HealthStatus.Healthy, (await CheckAsync()).Status);
    }

    // Nothing publishes the events: the outbox keeps growing.
    [Fact]
    public async Task NoRelayEverSeen_FailsWithTheFixInTheMessage()
    {
        Status(lastRelaySeen: null);

        var result = await CheckAsync();

        Assert.Equal(Defaults.OutboxHealthFailureStatus, result.Status);
        Assert.Contains(nameof(KaleidoServiceCollectionExtensions.AddOutboxRelay), result.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "Healthy")]
    [InlineData(1, "Degraded")]
    public async Task ARelayLastSeenAroundTheWindow(long ticksBeyondTheWindow, string expected)
    {
        Status(Now - Defaults.RelaySeenWithin - TimeSpan.FromTicks(ticksBeyondTheWindow));

        Assert.Equal(Enum.Parse<HealthStatus>(expected), (await CheckAsync()).Status);
    }

    [Theory]
    [InlineData(0, "Healthy")]
    [InlineData(1, "Degraded")]
    public async Task DueEntriesAroundTheMaximumCount(int beyond, string expected)
    {
        Status(Now, dueCount: Defaults.BacklogMaxCount + beyond, oldestDue: Now);

        Assert.Equal(Enum.Parse<HealthStatus>(expected), (await CheckAsync()).Status);
    }

    [Theory]
    [InlineData(0, "Healthy")]
    [InlineData(1, "Degraded")]
    public async Task TheOldestDueEntryAroundTheMaximumAge(long ticksBeyond, string expected)
    {
        Status(Now, dueCount: 1, oldestDue: Now - Defaults.BacklogMaxAge - TimeSpan.FromTicks(ticksBeyond));

        Assert.Equal(Enum.Parse<HealthStatus>(expected), (await CheckAsync()).Status);
    }

    // The host chooses whether a missing relay fails readiness.
    [Fact]
    public async Task TheConfiguredFailureStatus_IsReported()
    {
        Status(lastRelaySeen: null);

        Assert.Equal(HealthStatus.Unhealthy, (await CheckAsync(Defaults with { OutboxHealthFailureStatus = HealthStatus.Unhealthy })).Status);
    }

    [Fact]
    public async Task TheResult_CarriesWhatTheStoreReported()
    {
        Status(Now, dueCount: 3, oldestDue: Now);

        var result = await CheckAsync();

        Assert.Equal(3L, result.Data["dueCount"]);
        Assert.Equal(Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture), result.Data["lastRelaySeenUtc"]);
    }
}
