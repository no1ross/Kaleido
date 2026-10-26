using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessStateServiceTests
    : SutFixture
{
    private static ProcessStateService CreateSut(
        IKaleidoContextStore contextStore,
        IProcessorStepRegistry registry,
        IProcessorResponseFactory responseFactory,
        Microsoft.Extensions.Logging.ILogger<ProcessStateService>? logger = null) =>
        new(
            contextStore,
            registry,
            new KaleidoServiceOptions { ServiceName = "test-processor" },
            responseFactory,
            logger ?? NullLogger<ProcessStateService>.Instance);

    [Fact]
    public async Task GetCurrentState_WhenContextDoesNotExist_LogsLoadingAndNotFoundAtDebug()
    {
        var processId = Guid.Parse("4d7e2b1a-9c3f-4a8e-b6d5-1f2e3a4b5c6d");
        var logger = CreateLogger();

        await CreateSut(Mock.Of<IKaleidoContextStore>(), CreateRegistry(), Mock.Of<IProcessorResponseFactory>(), logger.Object)
            .GetCurrentState(processId, CancellationToken.None);

        Assert.Equal(
            [
                $"Loading process state for processor test-processor process {processId}.",
                $"Process state not found for processor test-processor process {processId}.",
            ],
            Logged(logger, Microsoft.Extensions.Logging.LogLevel.Debug));
    }

    [Fact]
    public async Task GetCurrentState_WhenContextExists_LogsTheLoadedStateAtDebug()
    {
        var processId = Guid.Parse("4d7e2b1a-9c3f-4a8e-b6d5-1f2e3a4b5c6d");
        var logger = CreateLogger();
        var contextStore = new Mock<IKaleidoContextStore>();
        contextStore
            .Setup(x => x.LoadAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessorContext { ProcessId = processId, ProcessorName = "test-processor", State = ProcessExecutionState.Complete });

        await CreateSut(contextStore.Object, CreateRegistry(), Mock.Of<IProcessorResponseFactory>(), logger.Object)
            .GetCurrentState(processId, CancellationToken.None);

        Assert.Equal(
            [
                $"Loading process state for processor test-processor process {processId}.",
                $"Process state loaded for processor test-processor process {processId} state Complete.",
            ],
            Logged(logger, Microsoft.Extensions.Logging.LogLevel.Debug));
    }

    [Fact]
    public async Task GetCurrentState_WhenHandedOff_ReturnsNoRequiredStep()
    {
        var responseFactory = new Mock<IProcessorResponseFactory>();
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
            State = ProcessExecutionState.AwaitingStepSelection,
            RequiredStep = "Step-B",
            TargetProcessorName = "radiology",
        };

        var result = await CreateSut(StoreReturning(context), CreateRegistry("Step-B"), responseFactory.Object)
            .GetCurrentState(context.ProcessId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.RequiredStep);
        Assert.Equal("radiology", result.TargetProcessorName);
        responseFactory.Verify(
            x => x.CreateRequiredStep(It.IsAny<ProcessorStepSummary>(), It.IsAny<InformationRequest?>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCurrentState_WithoutARequiredStep_ReturnsNoRequiredStep()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Complete,
        };

        var result = await CreateSut(StoreReturning(context), CreateRegistry(), Mock.Of<IProcessorResponseFactory>())
            .GetCurrentState(context.ProcessId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.RequiredStep);
        Assert.Empty(result.AvailableSteps);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public async Task GetCurrentState_WhenTheRequiredStepIsNotRegistered_ThrowsAFrameworkError()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
            RequiredStep = "Ghost-Step",
        };

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            CreateSut(StoreReturning(context), CreateRegistry(), Mock.Of<IProcessorResponseFactory>())
                .GetCurrentState(context.ProcessId, CancellationToken.None));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
        Assert.Contains("'Ghost-Step'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCurrentState_WhenAnAvailableStepIsNotRegistered_ThrowsAFrameworkError()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
            AvailableSteps = ["Ghost-Step"],
        };

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            CreateSut(StoreReturning(context), CreateRegistry(), Mock.Of<IProcessorResponseFactory>())
                .GetCurrentState(context.ProcessId, CancellationToken.None));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
        Assert.Contains("Available step 'Ghost-Step'", exception.Message, StringComparison.Ordinal);
    }

    private static IKaleidoContextStore StoreReturning(ProcessorContext context)
    {
        var store = new Mock<IKaleidoContextStore>();
        store.Setup(x => x.LoadAsync(context.ProcessId, It.IsAny<CancellationToken>())).ReturnsAsync(context);
        return store.Object;
    }

    private static Mock<Microsoft.Extensions.Logging.ILogger<ProcessStateService>> CreateLogger()
    {
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ProcessStateService>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<Microsoft.Extensions.Logging.LogLevel>())).Returns(true);
        return logger;
    }

    private static string?[] Logged(
        Mock<Microsoft.Extensions.Logging.ILogger<ProcessStateService>> logger,
        Microsoft.Extensions.Logging.LogLevel level) =>
        logger.Invocations
            .Where(x => string.Equals(x.Method.Name, "Log", StringComparison.Ordinal) && Equals(x.Arguments[0], level))
            .Select(x => x.Arguments[2]?.ToString())
            .ToArray();

    [Fact]
    public async Task GetCurrentState_WhenContextDoesNotExist_ReturnsNull()
    {
        var contextStore =
            new Mock<IKaleidoContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessorContext?)null);

        var service =
            CreateSut(
                contextStore.Object,
                CreateRegistry(),
                new Mock<IProcessorResponseFactory>().Object);

        var result =
            await service.GetCurrentState(
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentState_WhenContextExists_MapsView()
    {
        var processId =
            Guid.NewGuid();

        var context =
            new ProcessorContext
            {
                ProcessId = processId,
                ProcessorName = "test-processor",
                State = ProcessExecutionState.AwaitingStepSelection,
                RequiredStep = "Step-B",
                RequiredInformationRequest = PendingRequest,
                AvailableSteps = ["Step-A"],
                CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                UpdatedUtc = DateTimeOffset.UtcNow,
                CreatedBy = Alice,
                UpdatedBy = Bob,
                Steps =
                [
                    new StepContext
                    {
                        StepName = "Step-B",
                        Status = StepExecutionStatus.Pending
                    },
                    new StepContext
                    {
                        StepName = "Step-A",
                        Status = StepExecutionStatus.Completed,
                        FirstExecuted = FirstRun,
                        StartedBy = Alice,
                        LastExecuted = DateTimeOffset.UtcNow.AddMinutes(-1),
                        LastUpdatedBy = Bob,
                    }
                ]
            };

        var contextStore =
            new Mock<IKaleidoContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var expectedSummary = new ProcessStepSummary
        {
            Name = "Step-A"
        };

        var responseFactory = new Mock<IProcessorResponseFactory>();
        responseFactory
            .Setup(x => x.CreateStepSummary(
                It.IsAny<ProcessorStepSummary>(),
                "test-processor"))
            .Returns(expectedSummary);

        var expectedRequired = new ProcessRequiredStep
        {
            Name = "Step-B",
            InformationRequest = PendingRequest
        };

        responseFactory
            .Setup(x => x.CreateRequiredStep(
                It.Is<ProcessorStepSummary>(s => s.Name == "Step-B"),
                PendingRequest,
                "test-processor"))
            .Returns(expectedRequired);

        var service =
            CreateSut(
                contextStore.Object,
                CreateRegistry("Step-A", "Step-B"),
                responseFactory.Object);

        var result =
            await service.GetCurrentState(
                processId,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(processId, result.ProcessId);
        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
        Assert.Same(expectedRequired, result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Equal("Step-A", Assert.Single(result.AvailableSteps).Name);
        Assert.Equal(Alice, result.CreatedBy);
        Assert.Equal(Bob, result.UpdatedBy);

        Assert.Collection(
            result.Steps,
            step =>
            {
                Assert.Equal("Step-A", step.StepName);
                Assert.Equal(StepExecutionStatus.Completed, step.Status);
                Assert.Equal(FirstRun, step.FirstExecuted);
                Assert.Equal(Alice, step.StartedBy);
                Assert.Equal(Bob, step.LastUpdatedBy);
            },
            step =>
            {
                Assert.Equal("Step-B", step.StepName);
                Assert.Equal(StepExecutionStatus.Pending, step.Status);
            });
    }

    private static readonly KaleidoCaller Alice = new() { Name = "alice", Id = "sub-alice" };
    private static readonly KaleidoCaller Bob = new() { Name = "bob" };
    private static readonly DateTimeOffset FirstRun = DateTimeOffset.UtcNow.AddMinutes(-3);

    private static readonly InformationRequest PendingRequest =
        new()
        {
            InformationRequestId = "out-of-network",
            Items = [new InformationItem { Id = "q1", Text = "Proceed?", Type = InformationItemType.Boolean }]
        };

    private static IProcessorStepRegistry CreateRegistry(
        params string[] stepNames)
    {
        var mock = new Mock<IProcessorStepRegistry>();

        foreach (var name in stepNames)
        {
            var registration = new ProcessStepRegistration(
                typeof(object),
                null,
                typeof(object),
                [],
                [],
                [],
                new RepeatableOptions { Enabled = false },
                new ProcessStepMetadata { Name = name, Description = name, DisplayName = name });

            mock.Setup(x => x.Find(name))
                .Returns(registration);
        }

        return mock.Object;
    }
}
