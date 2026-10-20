using Kaleido.Eventing;
using Kaleido.Processor.Context;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Storage.UnitTests;

public sealed class InMemoryKaleidoContextStoreTests
    : SutFixture
{
    private static InMemoryKaleidoContextStore CreateSut(ILogger<InMemoryKaleidoContextStore>? logger = null) =>
        new(logger ?? new Mock<ILogger<InMemoryKaleidoContextStore>>().Object);

    [Fact]
    public async Task SaveAsync_WhenContextIsNull_Throws()
    {
        var store = CreateSut();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.SaveAsync(null!, []));
    }

    [Fact]
    public async Task SaveAsync_WhenContextProvided_SavesContext()
    {
        var store = CreateSut();
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Active
        };

        await store.SaveAsync(context, [], CancellationToken.None);

        var loaded = await store.LoadAsync(context.ProcessId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(context.ProcessId, loaded.ProcessId);
        Assert.Equal("test-processor", loaded.ProcessorName);
        Assert.Equal(ProcessExecutionState.Active, loaded.State);
    }

    [Fact]
    public async Task LoadAsync_WhenContextNotExists_ReturnsNull()
    {
        var store = CreateSut();
        var processId = Guid.NewGuid();

        var loaded = await store.LoadAsync(processId, CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadAsync_WhenContextExists_ReturnsContext()
    {
        var store = CreateSut();
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Complete,
            Steps =
            [
                new StepContext
                {
                    StepName = "test-step",
                    Version = "1.0",
                    Status = StepExecutionStatus.Completed,
                },
            ],
        };

        await store.SaveAsync(context, [], CancellationToken.None);
        var loaded = await store.LoadAsync(context.ProcessId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(context.ProcessId, loaded.ProcessId);
        Assert.Single(loaded.Steps);
        Assert.Equal("test-step", loaded.Steps.First().StepName);
    }

    [Fact]
    public async Task SaveAsync_WhenContextAlreadyExists_Overwrites()
    {
        var store = CreateSut();
        var processId = Guid.NewGuid();

        var context1 = new ProcessorContext
        {
            ProcessId = processId,
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Active
        };

        var context2 = new ProcessorContext
        {
            ProcessId = processId,
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Complete
        };

        var saved = await store.SaveAsync(context1, [], CancellationToken.None);
        await store.SaveAsync(context2 with { Version = saved.Version }, [], CancellationToken.None);

        var loaded = await store.LoadAsync(processId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(ProcessExecutionState.Complete, loaded.State);
    }

    [Fact]
    public async Task SaveAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var store = CreateSut();
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor"
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.SaveAsync(context, [], cts.Token));
    }

    [Fact]
    public async Task LoadAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var store = CreateSut();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.LoadAsync(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task SaveAsync_ReturnsTheContextWithTheNextVersion()
    {
        var store = CreateSut();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test-processor" };

        var first = await store.SaveAsync(context, [], CancellationToken.None);
        var second = await store.SaveAsync(first, [], CancellationToken.None);

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(2, (await store.LoadAsync(context.ProcessId, CancellationToken.None))!.Version);
    }

    [Fact]
    public async Task SaveAsync_WithAStaleVersion_ThrowsAConflictAndKeepsTheStoredState()
    {
        var store = CreateSut();
        var loaded = await store.SaveAsync(
            new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test-processor", State = ProcessExecutionState.Active },
            [],
            CancellationToken.None);

        await store.SaveAsync(loaded with { State = ProcessExecutionState.Complete }, [], CancellationToken.None);

        var exception = await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() =>
            store.SaveAsync(loaded with { State = ProcessExecutionState.Exception }, [], CancellationToken.None));

        Assert.Equal(loaded.ProcessId, exception.ProcessId);
        Assert.Equal(ProcessorErrorCodes.ProcessConflict, exception.Code);
        Assert.Equal(ProcessExecutionState.Complete, (await store.LoadAsync(loaded.ProcessId, CancellationToken.None))!.State);
    }

    [Fact]
    public async Task SaveAsync_ANewProcessWhoseIdAlreadyExists_ThrowsAConflict()
    {
        var store = CreateSut();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test-processor" };
        await store.SaveAsync(context, [], CancellationToken.None);

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() =>
            store.SaveAsync(context, [], CancellationToken.None));
    }

    private static IdempotentResult Admission(string key, DateTimeOffset? expiresUtc = null, string hash = "hash") =>
        new()
        {
            Key = key,
            RequestHash = hash,
            State = IdempotentResultState.InProgress,
            LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            ExpiresUtc = expiresUtc ?? DateTimeOffset.UtcNow.AddHours(1),
        };

    private static IdempotentResult Completed(string key, string response) =>
        new()
        {
            Key = key,
            RequestHash = "hash",
            State = IdempotentResultState.Completed,
            StatusCode = 200,
            Response = response,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        };

    [Fact]
    public async Task AdmitAsync_ANewKey_IsAdmitted_AndASecondAdmissionGetsTheRecordInProgress()
    {
        var sut = CreateSut();

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:k1")));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1", hash: "other"));
        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.InProgress, existing.State);
        Assert.Equal("hash", existing.RequestHash);
    }

    // #285: two attempts with one key at the same time never both run.
    [Fact]
    public async Task AdmitAsync_InParallel_AdmitsExactlyOne()
    {
        var sut = CreateSut();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => sut.AdmitAsync(Admission("svc:alice:race")))));

        Assert.Single(outcomes, x => x is null);
    }

    [Fact]
    public async Task CompleteAsync_ThenAdmitAsync_ReturnsTheCompletedAnswer()
    {
        var sut = CreateSut();
        await sut.AdmitAsync(Admission("svc:alice:k1"));

        await sut.CompleteAsync(Completed("svc:alice:k1", """{"processId":"p"}"""));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1"));
        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.Completed, existing.State);
        Assert.Equal(200, existing.StatusCode);
        Assert.Equal("""{"processId":"p"}""", existing.Response);
    }

    [Fact]
    public async Task ReleaseAsync_FreesAKeyInProgress_ButKeepsACompletedOne()
    {
        var sut = CreateSut();
        await sut.AdmitAsync(Admission("svc:alice:failed"));
        await sut.AdmitAsync(Admission("svc:alice:done"));
        await sut.CompleteAsync(Completed("svc:alice:done", "{}"));

        await sut.ReleaseAsync("svc:alice:failed");
        await sut.ReleaseAsync("svc:alice:done");

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:failed")));
        Assert.Equal(IdempotentResultState.Completed, (await sut.AdmitAsync(Admission("svc:alice:done")))?.State);
    }

    [Fact]
    public async Task AdmitAsync_OverAnExpiredRecord_AdmitsTheKeyAgain()
    {
        var sut = CreateSut();
        await sut.AdmitAsync(Admission("svc:alice:old", DateTimeOffset.UtcNow.AddMinutes(-1)));

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:old")));
    }

    [Fact]
    public async Task Constructor_WarnsOnce_AndEachSaveLogsAtDebug()
    {
        var logger = new Mock<ILogger<InMemoryKaleidoContextStore>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var store = CreateSut(logger.Object);
        await store.SaveAsync(NewProcess(), []);

        var levels = logger.Invocations.Where(x => x.Method.Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.Arguments[0]).ToList();
        Assert.Equal([LogLevel.Warning, LogLevel.Debug], levels);
        Assert.Contains("InMemoryKaleidoContextStore is active", logger.Invocations.First(x => x.Method.Name == nameof(ILogger.Log)).Arguments[2].ToString());
    }

    [Fact]
    public async Task CompleteEventAsync_AfterTheLeaseExpired_LeavesTheEntryForTheNextClaim()
    {
        var store = CreateSut();
        var @event = Event();
        await store.EnqueueEventsAsync([@event]);
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

    private static ProcessorContext NewProcess() =>
        new() { ProcessId = Guid.NewGuid(), ProcessorName = "test-processor" };

    [Fact]
    public async Task SaveAsync_WithEvents_AddsThemToTheOutbox()
    {
        var sut = CreateSut();
        var @event = Event();

        await sut.SaveAsync(NewProcess(), [@event]);

        var claimed = await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1));
        Assert.Equal(@event.EventId, Assert.Single(claimed).EventId);
    }

    [Fact]
    public async Task SaveAsync_WhenItConflicts_AddsNoEvents()
    {
        var sut = CreateSut();
        var context = NewProcess();
        await sut.SaveAsync(context, []);

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() => sut.SaveAsync(context, [Event()]));

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task ClaimEventsAsync_TakesAtMostTheBatchOldestFirstAndNeverTwice()
    {
        var sut = CreateSut();
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
        var sut = CreateSut();
        await sut.EnqueueEventsAsync([Event() with { NextAttemptUtc = DateTimeOffset.UtcNow.AddMinutes(1) }]);

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task ClaimEventsAsync_AfterTheLeaseExpired_ClaimsTheEntryAgain()
    {
        var sut = CreateSut();
        await sut.EnqueueEventsAsync([Event()]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        Assert.Single(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task CompleteEventAsync_RemovesTheLeasedEntry()
    {
        var sut = CreateSut();
        var @event = Event();
        await sut.EnqueueEventsAsync([@event]);
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
        await sut.EnqueueEventsAsync([@event]);
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
        await sut.EnqueueEventsAsync([@event]);
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
        await sut.EnqueueEventsAsync([@event]);
        await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1));

        await sut.FailEventAsync(@event.EventId, retryAtUtc: null, "gave up");

        Assert.Empty(await sut.ClaimEventsAsync(10, TimeSpan.FromMinutes(1)));
    }
}
