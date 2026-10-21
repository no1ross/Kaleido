using Kaleido.Eventing;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Storage.UnitTests;

public sealed class InMemoryKaleidoOutboxStoreTests
    : SutFixture
{
    private static InMemoryKaleidoOutboxStore CreateSut(ILogger<InMemoryKaleidoOutboxStore>? logger = null) =>
        new(logger ?? new Mock<ILogger<InMemoryKaleidoOutboxStore>>().Object);

    [Fact]
    public async Task CompleteEventAsync_AfterTheLeaseExpired_LeavesTheEntryForTheNextClaim()
    {
        var store = CreateSut();
        var @event = Event();
        await store.AddAsync([@event]);
        var claimed = Assert.Single(await store.ClaimEventsAsync(10, TimeSpan.Zero));

        await store.CompleteEventAsync(claimed.EventId);
        await store.FailEventAsync(claimed.EventId, retryAtUtc: null, "late");

        var reclaimed = Assert.Single(await store.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(@event.EventId, reclaimed.EventId);
        Assert.Equal(0, reclaimed.Attempts);
        Assert.False(reclaimed.IsParked);
    }

    private static KaleidoOutboxEvent Event(DateTimeOffset? occurredUtc = null) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventType = KaleidoEventTypes.ProcessStepCompleted,
            Payload = "{}",
            OccurredUtc = occurredUtc ?? DateTimeOffset.UtcNow.AddSeconds(-1),
            NextAttemptUtc = occurredUtc ?? DateTimeOffset.UtcNow.AddSeconds(-1),
        };

    [Fact]
    public async Task ClaimEventsAsync_TakesAtMostTheBatchOldestFirstAndNeverTwice()
    {
        var sut = CreateSut();
        var now = DateTimeOffset.UtcNow;
        var oldest = Event(now.AddSeconds(-3));
        var middle = Event(now.AddSeconds(-2));
        await sut.AddAsync([Event(now.AddSeconds(-1)), middle, oldest]);

        var first = await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1));
        var second = await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1));

        Assert.Equal([oldest.EventId, middle.EventId], first.Select(x => x.EventId));
        Assert.Single(second);
        Assert.Empty(await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task ClaimEventsAsync_SkipsEntriesThatAreNotDueYet()
    {
        var sut = CreateSut();
        await sut.AddAsync([Event() with { NextAttemptUtc = DateTimeOffset.UtcNow.AddMinutes(1) }]);

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task ClaimEventsAsync_AfterTheLeaseExpired_ClaimsTheEntryAgain()
    {
        var sut = CreateSut();
        await sut.AddAsync([Event()]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task CompleteEventAsync_RemovesTheLeasedEntry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMilliseconds(200));

        await sut.CompleteEventAsync(@event.EventId);
        await Task.Delay(250);

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task CompleteEventAsync_AfterTheLeaseExpired_KeepsTheEntry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        await sut.CompleteEventAsync(@event.EventId);

        Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task FailEventAsync_CountsTheAttemptAndReleasesTheEntryForTheRetry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1));

        await sut.FailEventAsync(@event.EventId, DateTimeOffset.UtcNow.AddSeconds(-1), "broker down");

        var retried = Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(1, retried.Attempts);
        Assert.Equal("broker down", retried.LastError);
        Assert.False(retried.IsParked);
    }

    [Fact]
    public async Task FailEventAsync_WithoutARetryTime_ParksTheEntry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.AddAsync([@event]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1));

        await sut.FailEventAsync(@event.EventId, retryAtUtc: null, "gave up");

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
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