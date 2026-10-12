using Kaleido.Processor.Context;
using Kaleido.Storage;
using Kaleido.Provider.SQLite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Provider.SQLite.UnitTests;

// The reference implementation's fidelity tests double as the executable
// IKaleidoContextStore contract spec - a consumer-authored store should
// satisfy the same shape.
public sealed class SqliteKaleidoContextStoreTests
    : Kaleido.UnitTests.SutFixture
{
    private static SqliteKaleidoContextDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new SqliteKaleidoContextDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static SqliteKaleidoContextStore CreateSut(SqliteKaleidoContextDbContext dbContext, Guid? instanceId = null) =>
        new(dbContext,
            new KaleidoServiceOptions { ServiceName = "test-svc", InstanceId = instanceId ?? Guid.NewGuid() },
            NullLogger<SqliteKaleidoContextStore>.Instance);

    private static ProcessorContext CreateContext(
        Guid processId,
        ProcessExecutionState state = ProcessExecutionState.Active) =>
        new()
        {
            ProcessId = processId,
            ProcessorName = "test-svc",
            LatestRequestId = "req-1",
            State = state,
            RequiredStep = "NextStep",
            TargetProcessorName = "target-svc",
            AvailableSteps = ["StepA", "StepB"],
            Steps =
            [
                new StepContext
                {
                    StepName = "StepA",
                    Version = "1.0.0",
                    Status = StepExecutionStatus.Completed,
                    LatestRequestId = "req-0",
                    LastExecuted = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
                },
                new StepContext
                {
                    StepName = "StepB",
                    Version = "1.0.0",
                    Status = StepExecutionStatus.Pending
                }
            ],
            CreatedUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
        };

    [Fact]
    public async Task LoadAsync_WhenProcessNotFound_ReturnsNull()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);

        var result = await sut.LoadAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAllFields()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var expected = CreateContext(Guid.NewGuid());

        await sut.SaveAsync(expected, []);

        var loaded = await sut.LoadAsync(expected.ProcessId);

        Assert.NotNull(loaded);
        Assert.Equal(expected.ProcessId, loaded.ProcessId);
        Assert.Equal(expected.ProcessorName, loaded.ProcessorName);
        Assert.Equal(expected.LatestRequestId, loaded.LatestRequestId);
        Assert.Equal(expected.State, loaded.State);
        Assert.Equal(expected.RequiredStep, loaded.RequiredStep);
        Assert.Equal(expected.TargetProcessorName, loaded.TargetProcessorName);
        Assert.Equal(expected.AvailableSteps, loaded.AvailableSteps);
        Assert.Equal(expected.CreatedUtc, loaded.CreatedUtc);
        Assert.Equal(expected.UpdatedUtc, loaded.UpdatedUtc);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsStepFidelity()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var expected = CreateContext(Guid.NewGuid());

        await sut.SaveAsync(expected, []);

        var loaded = await sut.LoadAsync(expected.ProcessId);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Steps.Count);

        var stepA = loaded.FindStep("StepA");
        Assert.NotNull(stepA);
        Assert.Equal("1.0.0", stepA.Version);
        Assert.Equal(StepExecutionStatus.Completed, stepA.Status);
        Assert.Equal("req-0", stepA.LatestRequestId);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), stepA.LastExecuted);

        var stepB = loaded.FindStep("StepB");
        Assert.NotNull(stepB);
        Assert.Equal(StepExecutionStatus.Pending, stepB.Status);
        Assert.Null(stepB.LatestRequestId);
        Assert.Null(stepB.LastExecuted);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsThePendingInformationRequest()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var expected =
            CreateContext(Guid.NewGuid(), ProcessExecutionState.AwaitingInformation) with
            {
                RequiredStep = "StepB",
                TargetProcessorName = null,
                RequiredInformationRequest = new InformationRequest
                {
                    InformationRequestId = "out-of-network",
                    Title = "Out-of-network attestation",
                    Items =
                    [
                        new InformationItem
                        {
                            Id = "reason",
                            Text = "Why?",
                            Type = InformationItemType.Choice,
                            Options = [new InformationOption { Value = "continuity", Display = "Continuity of care" }]
                        },
                        new InformationItem
                        {
                            Id = "details",
                            Text = "Details",
                            Type = InformationItemType.Group,
                            Items = [new InformationItem { Id = "visits", Text = "Visits", Type = InformationItemType.WholeNumber, Repeats = true }]
                        }
                    ]
                }
            };

        await sut.SaveAsync(expected, []);

        var loaded = await sut.LoadAsync(expected.ProcessId);

        Assert.NotNull(loaded?.RequiredInformationRequest);
        var request = loaded.RequiredInformationRequest;
        Assert.Equal("out-of-network", request.InformationRequestId);
        Assert.Equal("Out-of-network attestation", request.Title);
        Assert.Equal(InformationItemType.Choice, request.Items[0].Type);
        Assert.Equal("Continuity of care", Assert.Single(request.Items[0].Options).Display);
        var nested = Assert.Single(request.Items[1].Items);
        Assert.Equal(InformationItemType.WholeNumber, nested.Type);
        Assert.True(nested.Repeats);
    }

    [Fact]
    public async Task SaveAsync_WhenTheRequestIsAnswered_ClearsThePendingInformationRequest()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var processId = Guid.NewGuid();

        var saved = await sut.SaveAsync(
            CreateContext(processId) with
            {
                RequiredStep = "StepB",
                TargetProcessorName = null,
                RequiredInformationRequest = new InformationRequest { InformationRequestId = "out-of-network" }
            }, []);

        await sut.SaveAsync(
            CreateContext(processId) with
            {
                Version = saved.Version,
                RequiredStep = "StepB",
                TargetProcessorName = null,
                RequiredInformationRequest = null
            }, []);

        var loaded = await sut.LoadAsync(processId);

        Assert.NotNull(loaded);
        Assert.Equal("StepB", loaded.RequiredStep);
        Assert.Null(loaded.RequiredInformationRequest);
    }

    [Fact]
    public async Task SaveAsync_WhenSavedTwice_SecondSaveWins()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var processId = Guid.NewGuid();

        var saved = await sut.SaveAsync(CreateContext(processId, ProcessExecutionState.Active), []);

        var updated = CreateContext(processId, ProcessExecutionState.Complete) with
        {
            Version = saved.Version,
            RequiredStep = null,
            AvailableSteps = [],
            Steps = []
        };

        await sut.SaveAsync(updated, []);

        var loaded = await sut.LoadAsync(processId);

        Assert.NotNull(loaded);
        Assert.Equal(ProcessExecutionState.Complete, loaded.State);
        Assert.Null(loaded.RequiredStep);
        Assert.Empty(loaded.Steps);
        Assert.Empty(loaded.AvailableSteps);
    }

    [Fact]
    public async Task SaveAsync_WhenMultipleInstances_OnlyTargetedInstanceReturned()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var first = CreateContext(Guid.NewGuid());
        var second = CreateContext(Guid.NewGuid(), ProcessExecutionState.Complete);

        await sut.SaveAsync(first, []);
        await sut.SaveAsync(second, []);

        var loaded = await sut.LoadAsync(first.ProcessId);

        Assert.NotNull(loaded);
        Assert.Equal(first.ProcessId, loaded.ProcessId);
        Assert.Equal(ProcessExecutionState.Active, loaded.State);
    }

    [Fact]
    public async Task SaveAsync_ReturnsTheContextWithTheNextVersion_AndLoadReturnsIt()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var processId = Guid.NewGuid();

        var first = await sut.SaveAsync(CreateContext(processId), []);
        var second = await sut.SaveAsync(first with { State = ProcessExecutionState.Complete }, []);

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(2, (await sut.LoadAsync(processId))!.Version);
    }

    [Fact]
    public async Task SaveAsync_WithAStaleVersion_ThrowsAConflictAndKeepsTheStoredState()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var loaded = await sut.SaveAsync(CreateContext(Guid.NewGuid()), []);

        await sut.SaveAsync(loaded with { State = ProcessExecutionState.Complete }, []);

        var exception = await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() =>
            sut.SaveAsync(loaded with { State = ProcessExecutionState.Exception }, []));

        Assert.Equal(loaded.ProcessId, exception.ProcessId);
        Assert.Equal(ProcessExecutionState.Complete, (await sut.LoadAsync(loaded.ProcessId))!.State);
    }

    [Fact]
    public async Task SaveAsync_ANewProcessWhoseIdAlreadyExists_ThrowsAConflict()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var context = CreateContext(Guid.NewGuid());
        await sut.SaveAsync(context, []);

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() => sut.SaveAsync(context, []));
    }

    // Two service instances (separate DbContexts) on one database: both read version 1, the first
    // save wins, the second is refused instead of overwriting it.
    [Fact]
    public async Task SaveAsync_FromTwoInstancesOnOneDatabase_TheSecondIsRefused()
    {
        var connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var first = CreateSharedDbContext(connectionString);
        using var second = CreateSharedDbContext(connectionString);
        var processId = Guid.NewGuid();

        await CreateSut(first).SaveAsync(CreateContext(processId), []);
        var loadedByFirst = await CreateSut(first).LoadAsync(processId);
        var loadedBySecond = await CreateSut(second).LoadAsync(processId);

        await CreateSut(first).SaveAsync(loadedByFirst! with { State = ProcessExecutionState.Complete }, []);

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() =>
            CreateSut(second).SaveAsync(loadedBySecond! with { State = ProcessExecutionState.Exception }, []));
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

    private static IdempotentResult Result(string key, DateTimeOffset expiresUtc, string response = "{}") =>
        new() { Key = key, RequestHash = "hash", StatusCode = 200, Response = response, ExpiresUtc = expiresUtc };

    [Fact]
    public async Task SaveResultAsync_ThenFindResultAsync_ReturnsTheStoredAnswer()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var stored = Result("svc:alice:k1", DateTimeOffset.UtcNow.AddHours(1), """{"processId":"p"}""");

        await sut.SaveResultAsync(stored);

        var found = await sut.FindResultAsync("svc:alice:k1");
        Assert.NotNull(found);
        Assert.Equal(stored.RequestHash, found.RequestHash);
        Assert.Equal(stored.StatusCode, found.StatusCode);
        Assert.Equal(stored.Response, found.Response);
    }

    [Fact]
    public async Task FindResultAsync_ForAnExpiredOrUnknownKey_ReturnsNull()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        await sut.SaveResultAsync(Result("svc:alice:old", DateTimeOffset.UtcNow.AddMinutes(-1)));

        Assert.Null(await sut.FindResultAsync("svc:alice:old"));
        Assert.Null(await sut.FindResultAsync("svc:alice:unknown"));
    }

    [Fact]
    public async Task SaveResultAsync_ForAnExistingKey_ReplacesTheAnswer()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        await sut.SaveResultAsync(Result("svc:alice:k1", DateTimeOffset.UtcNow.AddHours(1), "first"));
        await sut.SaveResultAsync(Result("svc:alice:k1", DateTimeOffset.UtcNow.AddHours(1), "second"));

        Assert.Equal("second", (await sut.FindResultAsync("svc:alice:k1"))!.Response);
    }

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
    public async Task SaveAsync_WithEvents_StoresThemWithTheState()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var @event = Event();

        await sut.SaveAsync(CreateContext(Guid.NewGuid()), [@event]);

        var claimed = Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(@event.EventId, claimed.EventId);
        Assert.Equal(@event.EventType, claimed.EventType);
        Assert.Equal(@event.Payload, claimed.Payload);
        Assert.Equal(@event.OccurredUtc, claimed.OccurredUtc);
    }

    [Fact]
    public async Task SaveAsync_WhenItConflicts_StoresNoEvents()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var context = CreateContext(Guid.NewGuid());
        await sut.SaveAsync(context, []);

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() => sut.SaveAsync(context, [Event()]));

        dbContext.ChangeTracker.Clear();
        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task ClaimEventsAsync_TakesAtMostTheBatchOldestFirstAndNeverTwice()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext, Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var oldest = Event(now.AddSeconds(-3));
        var middle = Event(now.AddSeconds(-2));
        await sut.EnqueueEventsAsync([Event(now.AddSeconds(-1)), middle, oldest]);

        var first = await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1));
        var second = await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1));

        Assert.Equal([oldest.EventId, middle.EventId], first.Select(x => x.EventId));
        Assert.Single(second);
        Assert.Empty(await sut.ClaimEventsAsync(2, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task ClaimEventsAsync_SkipsEntriesThatAreNotDueYet()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        await sut.EnqueueEventsAsync([Event() with { NextAttemptUtc = DateTimeOffset.UtcNow.AddMinutes(1) }]);

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    // Two instances on one database claiming at the same time: each entry goes to exactly one.
    [Fact]
    public async Task ClaimEventsAsync_FromTwoInstancesOnOneDatabase_NeverClaimsAnEntryTwice()
    {
        var connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var first = CreateSharedDbContext(connectionString);
        using var second = CreateSharedDbContext(connectionString);
        var events = Enumerable.Range(0, 30).Select(i => Event(DateTimeOffset.UtcNow.AddSeconds(-30 + i))).ToArray();
        await CreateSut(first).EnqueueEventsAsync(events);
        var a = CreateSut(first, Guid.NewGuid());
        var b = CreateSut(second, Guid.NewGuid());

        var claimedByA = new List<Guid>();
        var claimedByB = new List<Guid>();

        for (var round = 0; round < 4; round++)
        {
            var claims = await Task.WhenAll(a.ClaimEventsAsync(5, TimeSpan.FromMinutes(1)), b.ClaimEventsAsync(5, TimeSpan.FromMinutes(1)));
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
        await sut.EnqueueEventsAsync([@event]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1));

        await sut.CompleteEventAsync(@event.EventId);

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
        await stale.EnqueueEventsAsync([@event]);

        await stale.ClaimEventsAsync(10, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);
        Assert.Single(await owner.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));

        await stale.CompleteEventAsync(@event.EventId);
        await stale.FailEventAsync(@event.EventId, retryAtUtc: null, "stale");

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
        await sut.EnqueueEventsAsync([@event]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1));

        await sut.FailEventAsync(@event.EventId, DateTimeOffset.UtcNow.AddSeconds(-1), "broker down");

        var retried = Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(1, retried.Attempts);
        Assert.Equal("broker down", retried.LastError);
    }

    [Fact]
    public async Task FailEventAsync_WithoutARetryTime_ParksTheEntryAndKeepsIt()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext, Guid.NewGuid());
        var @event = Event();
        await sut.EnqueueEventsAsync([@event]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1));

        await sut.FailEventAsync(@event.EventId, retryAtUtc: null, new string('x', 5000));

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
        var parked = Assert.Single(await dbContext.OutboxEvents.AsNoTracking().ToListAsync());
        Assert.True(parked.IsParked);
        Assert.Equal(SqliteKaleidoContextStore.MaxErrorLength, parked.LastError!.Length);
    }
}
