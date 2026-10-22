using Kaleido.Provider.SQLite.Entities;
using Kaleido.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kaleido.Provider.SQLite.UnitTests;

public sealed class SqliteKaleidoOutboxStoreTests
    : Kaleido.UnitTests.SutFixture
{
    private static SqliteKaleidoContextDbContext CreateDbContext(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>()
            .UseSqlite("DataSource=:memory:");

        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        var context = new SqliteKaleidoContextDbContext(builder.Options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static SqliteKaleidoContextDbContext CreateSharedDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var context = new SqliteKaleidoContextDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static SqliteKaleidoOutboxStore CreateSut(SqliteKaleidoContextDbContext dbContext, Guid? instanceId = null) =>
        new(dbContext, new KaleidoServiceOptions { ServiceName = "test-svc", InstanceId = instanceId ?? Guid.NewGuid() });

    private static KaleidoOutboxEvent Event(DateTimeOffset? occurredUtc = null)
    {
        var occurred = occurredUtc ?? DateTimeOffset.UtcNow.AddSeconds(-1);

        return new KaleidoOutboxEvent
        {
            EventId = Guid.NewGuid(),
            EventType = "process.step-completed.v1",
            Payload = """{"eventType":"process.step-completed.v1"}""",
            OccurredUtc = occurred,
            NextAttemptUtc = occurred,
        };
    }

    [Fact]
    public async Task ClaimEventsAsync_TakesAtMostTheBatchOldestFirstAndNeverTwice()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext, Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
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
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        await sut.AddAsync([Event() with { NextAttemptUtc = DateTimeOffset.UtcNow.AddMinutes(1) }], TestContext.Current.CancellationToken);

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    // Two instances on one database claiming at the same time: each entry goes to exactly one.
    [Fact]
    public async Task ClaimEventsAsync_FromTwoInstancesOnOneDatabase_NeverClaimsAnEntryTwice()
    {
        var connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var first = CreateSharedDbContext(connectionString);
        using var second = CreateSharedDbContext(connectionString);
        var events = Enumerable.Range(0, 30).Select(i => Event(DateTimeOffset.UtcNow.AddSeconds(-30 + i))).ToArray();
        await CreateSut(first).AddAsync(events, TestContext.Current.CancellationToken);
        var a = CreateSut(first, Guid.NewGuid());
        var b = CreateSut(second, Guid.NewGuid());

        var claimedByA = new List<Guid>();
        var claimedByB = new List<Guid>();

        for (var round = 0; round < 4; round++)
        {
            var claims = await Task.WhenAll(a.ClaimEventsAsync(5, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken), b.ClaimEventsAsync(5, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
            claimedByA.AddRange(claims[0].Select(x => x.EventId));
            claimedByB.AddRange(claims[1].Select(x => x.EventId));
        }

        Assert.Empty(claimedByA.Intersect(claimedByB));
        Assert.Equal(claimedByA.Count + claimedByB.Count, claimedByA.Concat(claimedByB).Distinct().Count());
        Assert.Equal(30, claimedByA.Count + claimedByB.Count);
    }

    [Fact]
    public async Task CompleteEventAsync_RemovesTheEntry()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext, Guid.NewGuid());
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        await sut.CompleteEventAsync(@event.EventId, TestContext.Current.CancellationToken);

        Assert.Empty(await dbContext.OutboxEvents.AsNoTracking().ToListAsync());
    }

    // An instance whose lease expired must not remove an entry another instance has claimed since.
    [Fact]
    public async Task CompleteEventAsync_ByAnInstanceWhoseLeaseExpired_KeepsTheEntryOfTheNewOwner()
    {
        var connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var first = CreateSharedDbContext(connectionString);
        using var second = CreateSharedDbContext(connectionString);
        var stale = CreateSut(first, Guid.NewGuid());
        var owner = CreateSut(second, Guid.NewGuid());
        var @event = Event();
        await stale.AddAsync([@event], TestContext.Current.CancellationToken);

        await stale.ClaimEventsAsync(10, TimeSpan.FromMilliseconds(1), TestContext.Current.CancellationToken);
        await Task.Delay(20);
        Assert.Single(await owner.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));

        await stale.CompleteEventAsync(@event.EventId, TestContext.Current.CancellationToken);
        await stale.FailEventAsync(@event.EventId, retryAtUtc: null, "stale", TestContext.Current.CancellationToken);

        var entry = Assert.Single(await second.OutboxEvents.AsNoTracking().ToListAsync());
        Assert.False(entry.IsParked);
        Assert.Equal(0, entry.Attempts);
    }

    [Fact]
    public async Task FailEventAsync_CountsTheAttemptAndReleasesTheEntryForTheRetry()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext, Guid.NewGuid());
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        await sut.FailEventAsync(@event.EventId, DateTimeOffset.UtcNow.AddSeconds(-1), "broker down", TestContext.Current.CancellationToken);

        var retried = Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        Assert.Equal(1, retried.Attempts);
        Assert.Equal("broker down", retried.LastError);
    }

    [Fact]
    public async Task FailEventAsync_WithoutARetryTime_ParksTheEntryAndKeepsIt()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext, Guid.NewGuid());
        var @event = Event();
        await sut.AddAsync([@event], TestContext.Current.CancellationToken);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        await sut.FailEventAsync(@event.EventId, retryAtUtc: null, new string('x', 5000), TestContext.Current.CancellationToken);

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        var parked = Assert.Single(await dbContext.OutboxEvents.AsNoTracking().ToListAsync());
        Assert.True(parked.IsParked);
        Assert.Equal(SqliteKaleidoOutboxStore.MaxErrorLength, parked.LastError!.Length);
    }

    // The entries SqliteKaleidoContextStore adds with a save are rows of the same OutboxEvents table:
    // this store claims them like enqueued ones (the shared-database rule, ADR 0020).
    [Fact]
    public async Task ClaimEventsAsync_ClaimsARowWrittenWithASave()
    {
        using var dbContext = CreateDbContext();
        var @event = Event();
        dbContext.OutboxEvents.Add(@event.ToEntity());
        await dbContext.SaveChangesAsync();

        var claimed = Assert.Single(await CreateSut(dbContext).ClaimEventsAsync(10, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));

        Assert.Equal(@event.EventId, claimed.EventId);
        Assert.Equal(@event.Payload, claimed.Payload);
        Assert.Equal(@event.OccurredUtc, claimed.OccurredUtc);
    }

    [Fact]
    public async Task AddAsync_WithNullEvents_Throws()
    {
        using var dbContext = CreateDbContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut(dbContext).AddAsync(null!, TestContext.Current.CancellationToken));
    }
}
