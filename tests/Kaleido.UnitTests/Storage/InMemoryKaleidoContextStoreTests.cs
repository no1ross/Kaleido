using Kaleido.Eventing;
using Kaleido.Processor.Context;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Storage.UnitTests;

public sealed class InMemoryKaleidoContextStoreTests
    : SutFixture
{
    private readonly Mock<IKaleidoOutboxStore> outbox = new();

    private InMemoryKaleidoContextStore CreateSut(ILogger<InMemoryKaleidoContextStore>? logger = null) =>
        new(logger ?? new Mock<ILogger<InMemoryKaleidoContextStore>>().Object, outbox.Object);

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
    public async Task SaveAsync_WithEvents_AddsThemToTheOutboxAfterTheState()
    {
        var sut = CreateSut();
        var @event = Event();

        await sut.SaveAsync(NewProcess(), [@event]);

        outbox.Verify(x => x.EnqueueEventsAsync(It.Is<IReadOnlyCollection<KaleidoOutboxEvent>>(e => e.Single().EventId == @event.EventId), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WhenItConflicts_AddsNoEvents()
    {
        var sut = CreateSut();
        var context = NewProcess();
        await sut.SaveAsync(context, []);

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() => sut.SaveAsync(context, [Event()]));

        outbox.Verify(x => x.EnqueueEventsAsync(It.Is<IReadOnlyCollection<KaleidoOutboxEvent>>(e => e.Count > 0), It.IsAny<CancellationToken>()), Times.Never);
    }
}
