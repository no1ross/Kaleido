using Kaleido.Eventing;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Storage.UnitTests;

public sealed class InMemoryKaleidoOutboxStoreTests
    : SutFixture
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    private InMemoryKaleidoOutboxStore CreateSut(ILogger<InMemoryKaleidoOutboxStore>? logger = null) =>
        new(time, logger ?? new Mock<ILogger<InMemoryKaleidoOutboxStore>>().Object);

    [Fact]
    public async Task CompleteEventAsync_AfterTheLeaseExpired_LeavesTheEntryForTheNextClaim()
    {
        var store = CreateSut();
        var @event = Event();
        await store.AddAsync([@event], TestContext.Current.CancellationToken);
        var claimed = Assert.Single(await store.ClaimEventsAsync(10, TimeSpan.Zero, TestContext.Current.CancellationToken));

        await store.CompleteEventAsync(claimed.EventId, TestContext.Current.CancellationToken);
        await store.FailEventAsync(claimed.EventId, retryAtUtc: null, "late", TestContext.Current.CancellationToken);

        var reclaimed = Assert.Single(await store.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        Assert.Equal(@event.EventId, reclaimed.EventId);
        Assert.Equal(0, reclaimed.Attempts);
        Assert.False(reclaimed.IsParked);
    }

    private KaleidoOutboxEvent Event(DateTimeOffset? occurredUtc = null) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventType = KaleidoEventTypes.ProcessStepCompleted,
            Payload = "{}",
            OccurredUtc = occurredUtc ?? time.GetUtcNow().AddSeconds(-1),
            NextAttemptUtc = occurredUtc ?? time.GetUtcNow().AddSeconds(-1),
        };

    [Fact]
    public async Task ClaimEventsAsync_TakesAtMostTheBatchOldestFirstAndNeverTwice()
    {
        var sut = CreateSut();
        var now = time.GetUtcNow();
        var oldest = Event(now.AddSeconds(-3));
        var middle = Event(now.AddSeconds(-2));
        await sut.AddAsync([Event(now.AddSeconds(-1)), middle, oldest], TestContext.Current.CancellationToken);

        var first = await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        var second = await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        Assert.Equal([oldest.EventId, middle.EventId], first.Select(x => x.EventId));
        Assert.Single(second);
        Assert.Empty(await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ClaimEventsAsync_SkipsEntriesThatAreNotDueYet()
    {
        var sut = CreateSut();
        await sut.AddAsync([Event() with { NextAttemptUtc = time.GetUtcNow().AddMinutes(1) }], TestContext.Current.CancellationToken);

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ClaimEventsAsync_AfterTheLeaseExpired_ClaimsTheEntryAgain()
    {
        var sut = CreateSut();
        await sut.AddAsync([Event()], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    // The lease is held until its end: just before it the entry is not claimed again, at its end it is.
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public async Task ClaimEventsAsync_AroundTheEndOfTheLease(int ticksFromTheEnd, int claimed)
    {
        var sut = CreateSut();
        await sut.AddAsync([Event()], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        time.Advance(TimeSpan.FromMinutes(1) + (Tick * ticksFromTheEnd));

        Assert.Equal(claimed, (await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)).Count);
    }

    // A completion counts while the lease is held, up to its last tick.
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    public async Task CompleteEventAsync_AroundTheEndOfTheLease(int ticksFromTheEnd, int left)
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(1) + (Tick * ticksFromTheEnd));

        await sut.CompleteEventAsync(@event.EventId, TestContext.Current.CancellationToken);

        Assert.Equal(left, (await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)).Count);
    }

    // A retry is due at its time, not before.
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public async Task ClaimEventsAsync_AroundTheRetryTime(int ticksFromTheRetry, int claimed)
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        var retryAt = time.GetUtcNow().AddSeconds(30);
        await sut.FailEventAsync(@event.EventId, retryAt, "broker down", TestContext.Current.CancellationToken);

        time.SetUtcNow(retryAt + (Tick * ticksFromTheRetry));

        Assert.Equal(claimed, (await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)).Count);
    }

    // An event the store does not hold (completed by another attempt, or never added) is ignored.
    [Fact]
    public async Task CompleteEventAsync_AndFailEventAsync_ForAnUnknownEvent_ChangeNothing()
    {
        var sut = CreateSut();
        await sut.AddAsync([Event()], TestContext.Current.CancellationToken);

        await sut.CompleteEventAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        await sut.FailEventAsync(Guid.NewGuid(), retryAtUtc: null, "unknown", TestContext.Current.CancellationToken);

        Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteEventAsync_RemovesTheLeasedEntry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        await sut.CompleteEventAsync(@event.EventId, TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteEventAsync_AfterTheLeaseExpired_KeepsTheEntry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromMinutes(1));

        await sut.CompleteEventAsync(@event.EventId, TestContext.Current.CancellationToken);

        Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FailEventAsync_CountsTheAttemptAndReleasesTheEntryForTheRetry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        await sut.FailEventAsync(@event.EventId, time.GetUtcNow().AddSeconds(-1), "broker down", TestContext.Current.CancellationToken);

        var retried = Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        Assert.Equal(1, retried.Attempts);
        Assert.Equal("broker down", retried.LastError);
        Assert.False(retried.IsParked);
    }

    [Fact]
    public async Task FailEventAsync_WithoutARetryTime_ParksTheEntry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        await sut.FailEventAsync(@event.EventId, retryAtUtc: null, "gave up", TestContext.Current.CancellationToken);

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_WarnsOnceThatTheStoreIsInMemoryOnly()
    {
        var logger = new Mock<ILogger<InMemoryKaleidoOutboxStore>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _ = CreateSut(logger.Object);

        var log = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Warning, (LogLevel)log.Arguments[0]);
        Assert.Contains("InMemoryKaleidoOutboxStore is active", log.Arguments[2].ToString());
    }
}
