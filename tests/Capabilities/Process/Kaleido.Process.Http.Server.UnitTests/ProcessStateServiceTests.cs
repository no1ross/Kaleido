using Kaleido.Observability;
using Kaleido.Process.Context;
using Kaleido.Process.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Process.Http.Server.UnitTests;

public sealed class ProcessStateServiceTests : Kaleido.UnitTests.SutFixture
{
    private const string Processor = "test-processor";
    private static readonly KaleidoCaller Alice = new() { Name = "alice", Id = "sub-alice" };
    private static readonly KaleidoCaller Bob = new() { Name = "bob" };
    private static readonly DateTimeOffset FirstRun = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly InformationRequest PendingRequest = new()
    {
        InformationRequestId = "out-of-network",
        Items = [new InformationItem { Id = "q1", Text = "Proceed?", Type = InformationItemType.Boolean }],
    };

    private static ProcessStateService CreateSut(
        IProcessContextStore contextStore,
        ILogger<ProcessStateService>? logger = null,
        IProcessContinuationFilter? continuations = null,
        string serviceVersion = "1") =>
        new(contextStore, new KaleidoServiceOptions { ServiceName = Processor, ServiceVersion = serviceVersion },
            continuations ?? PassThrough(), logger ?? NullLogger<ProcessStateService>.Instance);

    // The continuation filter (its own fixture) leaves the state as it is.
    private static IProcessContinuationFilter PassThrough()
    {
        var filter = new Mock<IProcessContinuationFilter>();
        filter.Setup(x => x.FilterAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string? required, IReadOnlyCollection<string> available, string? _, CancellationToken _) =>
                new ProcessContinuation(required, available, Notice: null));
        return filter.Object;
    }

    [Fact]
    public async Task GetCurrentState_WhenContextDoesNotExist_LogsLoadingAndNotFoundAtDebug()
    {
        var processId = Guid.Parse("4d7e2b1a-9c3f-4a8e-b6d5-1f2e3a4b5c6d");
        var logger = CreateLogger();

        await CreateSut(Mock.Of<IProcessContextStore>(), logger.Object).GetCurrentState(processId, CancellationToken.None);

        Assert.Equal(
        [
            $"Loading process state for processor test-processor process {processId}.",
            $"Process state not found for processor test-processor process {processId}.",
        ], Logged(logger, LogLevel.Debug));
    }

    [Fact]
    public async Task GetCurrentState_WhenContextExists_LogsTheLoadedStateAtDebug()
    {
        var processId = Guid.Parse("4d7e2b1a-9c3f-4a8e-b6d5-1f2e3a4b5c6d");
        var logger = CreateLogger();
        var context = new ProcessContext { ProcessId = processId, ProcessorName = Processor, ProcessorVersion = "1", State = ProcessExecutionState.Complete };

        await CreateSut(StoreReturning(context), logger.Object).GetCurrentState(processId, CancellationToken.None);

        Assert.Equal(
        [
            $"Loading process state for processor test-processor process {processId}.",
            $"Process state loaded for processor test-processor process {processId} state Complete.",
        ], Logged(logger, LogLevel.Debug));
    }

    [Fact]
    public async Task GetCurrentState_WhenHandedOff_ReturnsNoRequiredStep()
    {
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = Processor,
            ProcessorVersion = "1",
            State = ProcessExecutionState.HandOff,
            RequiredStep = "Step-B",
            TargetProcessorName = "radiology",
            TargetProcessorVersion = "2",
        };

        var result = await CreateSut(StoreReturning(context)).GetCurrentState(context.ProcessId, CancellationToken.None);

        var snapshot = Assert.IsType<ProcessStateSnapshot>(result);
        Assert.Null(snapshot.RequiredStep);
        Assert.Equal("radiology", snapshot.TargetProcessorName);
        Assert.Equal("2", snapshot.TargetProcessorVersion);
    }

    // #524: the state describes the caller only what the continuation filter permits, with its notice.
    [Fact]
    public async Task GetCurrentState_DescribesTheCallerOnlyWhatTheFilterPermits()
    {
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = Processor,
            ProcessorVersion = "1",
            State = ProcessExecutionState.AwaitingInformation,
            RequiredStep = "SecretStep",
            RequiredInformationRequest = PendingRequest,
            AvailableSteps = ["Review"],
        };
        var filter = new Mock<IProcessContinuationFilter>();
        filter.Setup(x => x.FilterAsync(context.ProcessId, "SecretStep", It.IsAny<IReadOnlyCollection<string>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessContinuation(RequiredStep: null, AvailableSteps: [], ProcessContinuationFilter.NotPermitted));

        var result = await CreateSut(StoreReturning(context), continuations: filter.Object).GetCurrentState(context.ProcessId, CancellationToken.None);

        var snapshot = Assert.IsType<ProcessStateSnapshot>(result);
        Assert.Equal(ProcessContinuationFilter.NotPermitted, snapshot.Notice);
        Assert.Null(snapshot.RequiredStep);
        Assert.Null(snapshot.RequiredInformationRequest);
        Assert.Empty(snapshot.AvailableSteps);
    }

    [Fact]
    public async Task GetCurrentState_WithoutARequiredStep_ReturnsNoRequiredStep()
    {
        var context = new ProcessContext { ProcessId = Guid.NewGuid(), ProcessorName = Processor, ProcessorVersion = "1", State = ProcessExecutionState.Complete };

        var result = await CreateSut(StoreReturning(context)).GetCurrentState(context.ProcessId, CancellationToken.None);

        var snapshot = Assert.IsType<ProcessStateSnapshot>(result);
        Assert.Null(snapshot.RequiredStep);
        Assert.Empty(snapshot.AvailableSteps);
        Assert.Empty(snapshot.Steps);
    }

    [Fact]
    public async Task GetCurrentState_WhenContextDoesNotExist_ReturnsNull()
    {
        var contextStore = new Mock<IProcessContextStore>();
        contextStore.Setup(x => x.LoadAsync(It.IsAny<ProcessKey>(), It.IsAny<CancellationToken>())).ReturnsAsync((ProcessContext?)null);

        Assert.Null(await CreateSut(contextStore.Object).GetCurrentState(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentState_WhenContextExists_MapsView()
    {
        var processId = Guid.NewGuid();
        var context = new ProcessContext
        {
            ProcessId = processId,
            ProcessorName = Processor,
            ProcessorVersion = "1",
            State = ProcessExecutionState.AwaitingStepSelection,
            RequiredStep = "Step-B",
            RequiredInformationRequest = PendingRequest,
            AvailableSteps = ["Step-A"],
            CreatedUtc = FirstRun.AddMinutes(-5),
            UpdatedUtc = FirstRun,
            CreatedBy = Alice,
            UpdatedBy = Bob,
            Steps =
            [
                new StepContext { StepName = "Step-B", Status = StepExecutionStatus.Pending },
                new StepContext { StepName = "Step-A", Status = StepExecutionStatus.Completed, FirstExecuted = FirstRun, StartedBy = Alice, LastExecuted = FirstRun, LastUpdatedBy = Bob },
            ],
        };

        var result = await CreateSut(StoreReturning(context)).GetCurrentState(processId, CancellationToken.None);

        var snapshot = Assert.IsType<ProcessStateSnapshot>(result);
        Assert.Equal(processId, snapshot.ProcessId);
        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, snapshot.State);
        Assert.Equal("Step-B", snapshot.RequiredStep);
        Assert.Same(PendingRequest, snapshot.RequiredInformationRequest);
        Assert.Equal(["Step-A"], snapshot.AvailableSteps);
        Assert.Same(Alice, snapshot.CreatedBy);
        Assert.Same(Bob, snapshot.UpdatedBy);
        Assert.Collection(snapshot.Steps,
            step =>
            {
                Assert.Equal("Step-A", step.StepName);
                Assert.Equal(StepExecutionStatus.Completed, step.Status);
                Assert.Equal(FirstRun, step.FirstExecuted);
                Assert.Same(Alice, step.StartedBy);
                Assert.Same(Bob, step.LastUpdatedBy);
            },
            step =>
            {
                Assert.Equal("Step-B", step.StepName);
                Assert.Equal(StepExecutionStatus.Pending, step.Status);
            });
    }

    private static IProcessContextStore StoreReturning(ProcessContext context)
    {
        var store = new Mock<IProcessContextStore>();
        store.Setup(x => x.LoadAsync(context.Key, It.IsAny<CancellationToken>())).ReturnsAsync(context);
        return store.Object;
    }

    private static Mock<ILogger<ProcessStateService>> CreateLogger()
    {
        var logger = new Mock<ILogger<ProcessStateService>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    private static string?[] Logged(Mock<ILogger<ProcessStateService>> logger, LogLevel level) =>
        logger.Invocations
            .Where(x => string.Equals(x.Method.Name, "Log", StringComparison.Ordinal) && Equals(x.Arguments[0], level))
            .Select(x => x.Arguments[2]?.ToString())
            .ToArray();

    // The state is read under this service's name and version (#552): another version's process is
    // not found here.
    [Fact]
    public async Task GetCurrentState_ReadsTheProcessUnderThisServicesVersion()
    {
        const string version = "2";
        var processId = Guid.NewGuid();
        var contextStore = new Mock<IProcessContextStore>();

        var state = await CreateSut(contextStore.Object, serviceVersion: version).GetCurrentState(processId, TestContext.Current.CancellationToken);

        Assert.Null(state);
        contextStore.Verify(x => x.LoadAsync(new ProcessKey(Processor, version, processId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
