using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;
using Kaleido.Processor.Registry;
using Microsoft.Extensions.Logging.Abstractions;

using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests.Processor.Execution;

public sealed class ProcessorExecutorTests
    : SutFixture
{
    [Fact]
    public async Task ExecuteAsync_WhenNoCandidates_ReturnsCurrentContextState()
    {
        var context =
            CreateContext("step-a") with
            {
                AvailableSteps = ["step-a", "step-b"],
                RequiredStep = "step-a"
            };

        var invoker =
            new Mock<IProcessStepInvoker>();

        var processor =
            CreateSut(invoker);

        var result =
            await processor.ExecuteAsync(
                [],
                context,
                new ProcessorRequest());

        Assert.Equal(
            context.State,
            result.State);

        Assert.Equal(
            context.RequiredStep,
            result.RequiredStep);

        Assert.Equal(
            context.AvailableSteps,
            result.AvailableSteps);

        Assert.Empty(
            result.Outcomes);

        invoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_PassesAvailableNextStepsToProcessStepContext()
    {
        var registration =
            CreateRegistration<TestStepA>("step-a");

        var candidate =
            CreateCandidate<TestStepA>("step-a", registration);

        var context =
            CreateContext("step-a");

        ProcessStepContext? capturedContext =
            null;

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    candidate.Registration!,
                    candidate.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .Callback<ProcessStepRegistration, object, ProcessStepContext, CancellationToken>(
                (_, _, processStepContext, _) =>
                    capturedContext = processStepContext)
            .ReturnsAsync(CreateInvokerResult());

        var availabilityResolver =
            new Mock<IStepAvailabilityResolver>();

        availabilityResolver
            .Setup(x =>
                x.Resolve(
                    candidate,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context))
            .Returns(["step-b"]);

        var processor =
            CreateSut(invoker, availabilityResolver);

        await processor.ExecuteAsync(
            [candidate],
            context,
            new ProcessorRequest());

        Assert.NotNull(
            capturedContext);

        Assert.Contains(
            capturedContext.AvailableNextSteps,
            x => x == "step-b");
    }

    [Fact]
    public async Task ExecuteAsync_InvokesCurrentCandidate()
    {
        var registration =
            CreateRegistration<TestStepA>("step-a");

        var candidate =
            CreateCandidate<TestStepA>("step-a", registration);

        var context =
            CreateContext("step-a");

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    candidate.Registration!,
                    candidate.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInvokerResult());

        var processor =
            CreateSut(invoker);

        await processor.ExecuteAsync(
            [candidate],
            context,
            new ProcessorRequest());

        invoker.Verify(
            x =>
                x.ExecuteAsync(
                    candidate.Registration!,
                    candidate.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EvaluatesInvokerResult()
    {
        var registration =
            CreateRegistration<TestStepA>("step-a");

        var candidate =
            CreateCandidate<TestStepA>("step-a", registration);

        var context =
            CreateContext("step-a");

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInvokerResult());

        var processor =
            CreateSut(invoker);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest());

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            ExecutionDecisionType.Complete,
            outcome.Decision);

        Assert.Equal(
            StepExecutionStatus.Completed,
            outcome.Status);
    }

    [Fact]
    public async Task ExecuteAsync_AppliesExecutionAndPersistsUpdatedContext()
    {
        var registration =
            CreateRegistration<TestStepA>("step-a");

        var candidate =
            CreateCandidate<TestStepA>("step-a", registration);

        var context =
            CreateContext("step-a");

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInvokerResult());

        var expectedContext =
            context with
            {
                State = ProcessExecutionState.Complete,
                RequiredStep = null
            };

        var stateUpdater =
            new Mock<IProcessorStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    context,
                    candidate,
                    It.IsAny<ExecutionDecision>()))
            .Returns(expectedContext);

        var savedContexts =
            new List<ProcessorContext>();

        var store =
            CreateStore(savedContexts);

        var processor =
            CreateSut(
                invoker,
                stateUpdater: stateUpdater,
                store: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest());

        var persisted =
            Assert.Single(savedContexts);

        Assert.Same(
            expectedContext,
            persisted);

        Assert.Equal(
            ProcessExecutionState.Complete,
            persisted.State);

        Assert.Equal(
            result.State,
            persisted.State);

        Assert.Null(persisted.RequiredStep);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDecisionIsComplete_ReturnsSingleCompletedOutcome()
    {
        var registration =
            CreateRegistration<TestStepA>("step-a");

        var candidate =
            CreateCandidate<TestStepA>("step-a", registration);

        var response =
            new TestStepResponse();

        var context =
            CreateContext("step-a");

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInvokerResult(response));

        var processor =
            CreateSut(invoker);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest());

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            "step-a",
            outcome.StepName);

        Assert.Equal(
            StepExecutionStatus.Completed,
            outcome.Status);

        Assert.Equal(
            ExecutionDecisionType.Complete,
            outcome.Decision);

        Assert.Same(
            response,
            outcome.Response);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDecisionIsContinue_ExecutesNextCandidate()
    {
        var registrationA =
            CreateRegistration<TestStepA>("step-a");

        var registrationB =
            CreateRegistration<TestStepB>("step-b");

        var firstCandidate =
            CreateCandidate<TestStepA>("step-a", registrationA);

        var nextCandidate =
            CreateCandidate<TestStepB>("step-b", registrationB);

        var context =
            CreateContext("step-a", "step-b");

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .SetupSequence(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInvokerResult())
            .ReturnsAsync(CreateInvokerResult());

        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        evaluator
            .SetupSequence(x =>
                x.Evaluate(
                    It.IsAny<StepCandidate>(),
                    It.IsAny<StepInvocationResult>(),
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>()))
            .Returns(ExecutionDecision.Continue(nextCandidate))
            .Returns(ExecutionDecision.Complete());

        var activeContext =
            context with { State = ProcessExecutionState.Active };

        var completeContext =
            context with { State = ProcessExecutionState.Complete };

        var stateUpdater =
            new Mock<IProcessorStateUpdater>();

        stateUpdater
            .SetupSequence(x =>
                x.ApplyExecution(
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<StepCandidate>(),
                    It.IsAny<ExecutionDecision>()))
            .Returns(activeContext)
            .Returns(completeContext);

        var savedContexts =
            new List<ProcessorContext>();

        var store =
            CreateStore(savedContexts);

        var processor =
            CreateSut(
                invoker,
                evaluator: evaluator,
                stateUpdater: stateUpdater,
                store: store);

        var result =
            await processor.ExecuteAsync(
                [firstCandidate, nextCandidate],
                context,
                new ProcessorRequest());

        Assert.Collection(
            result.Outcomes,
            first =>
            {
                Assert.Equal(
                    "step-a",
                    first.StepName);

                Assert.Equal(
                    ExecutionDecisionType.Continue,
                    first.Decision);
            },
            second =>
            {
                Assert.Equal(
                    "step-b",
                    second.StepName);

                Assert.Equal(
                    ExecutionDecisionType.Complete,
                    second.Decision);
            });

        invoker.Verify(
            x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        var persisted =
            Assert.IsType<ProcessorContext>(
                savedContexts[^1]);

        Assert.Equal(
            ProcessExecutionState.Complete,
            persisted.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvokerThrows_AppliesExceptionPersistsAndReturnsExceptionOutcome()
    {
        var registration =
            CreateRegistration<TestStepA>("step-a");

        var candidate =
            CreateCandidate<TestStepA>("step-a", registration);

        var context =
            CreateContext("step-a");

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var savedContexts =
            new List<ProcessorContext>();

        var store =
            CreateStore(savedContexts);

        var processor =
            CreateSut(invoker, store: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest());

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            "step-a",
            outcome.StepName);

        Assert.Equal(
            StepExecutionStatus.Exception,
            outcome.Status);

        Assert.Equal(
            ExecutionDecisionType.ProcessViolation,
            outcome.Decision);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == StepProcessingMessageCode.FrameworkException);

        var persisted =
            Assert.Single(savedContexts);

        Assert.Equal(
            ProcessExecutionState.Exception,
            persisted.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_AppliesCancellationPersistsAndReturnsCanceledOutcome()
    {
        var registration =
            CreateRegistration<TestStepA>("step-a");

        var candidate =
            CreateCandidate<TestStepA>("step-a", registration);

        var context =
            CreateContext("step-a");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        var invoker =
            new Mock<IProcessStepInvoker>();

        var savedContexts =
            new List<ProcessorContext>();

        var store =
            CreateStore(savedContexts);

        var processor =
            CreateSut(invoker, store: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest(),
                cancellationTokenSource.Token);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            "step-a",
            outcome.StepName);

        Assert.Equal(
            StepExecutionStatus.Canceled,
            outcome.Status);

        Assert.Equal(
            ExecutionDecisionType.ProcessViolation,
            outcome.Decision);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == StepProcessingMessageCode.ExecutionCanceled);

        invoker.Verify(
            x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        var persisted =
            Assert.Single(savedContexts);

        Assert.Equal(
            ProcessExecutionState.Canceled,
            persisted.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidateRegistrationIsMissing_ReturnsExceptionOutcome()
    {
        var candidate =
            CreateCandidate(
                "step-a",
                registration: null,
                step: new TestStepA());

        var context =
            CreateContext("step-a");

        var invoker =
            new Mock<IProcessStepInvoker>();

        var savedContexts =
            new List<ProcessorContext>();

        var store =
            CreateStore(savedContexts);

        var processor =
            CreateSut(invoker, store: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest());

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            StepExecutionStatus.Exception,
            outcome.Status);

        Assert.Equal(
            ExecutionDecisionType.ProcessViolation,
            outcome.Decision);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == StepProcessingMessageCode.FrameworkException);

        var persisted =
            Assert.Single(savedContexts);

        Assert.Equal(
            ProcessExecutionState.Exception,
            persisted.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidateStepIsMissing_ReturnsExceptionOutcome()
    {
        var candidate =
            CreateCandidate(
                "step-a",
                CreateRegistration<TestStepA>("step-a"),
                step: null);

        var context =
            CreateContext("step-a");

        var invoker =
            new Mock<IProcessStepInvoker>();

        var savedContexts =
            new List<ProcessorContext>();

        var store =
            CreateStore(savedContexts);

        var processor =
            CreateSut(invoker, store: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest());

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            StepExecutionStatus.Exception,
            outcome.Status);

        Assert.Equal(
            ExecutionDecisionType.ProcessViolation,
            outcome.Decision);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == StepProcessingMessageCode.FrameworkException);

        var persisted =
            Assert.Single(savedContexts);

        Assert.Equal(
            ProcessExecutionState.Exception,
            persisted.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepContextIsMissing_ReturnsExceptionOutcome()
    {
        var candidate =
            CreateCandidate<TestStepA>("step-a");

        var context =
            CreateContext("different-step");

        var invoker =
            new Mock<IProcessStepInvoker>();

        var stateUpdater =
            new Mock<IProcessorStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyException(
                    context,
                    candidate))
            .Returns(context with { State = ProcessExecutionState.Exception });

        var processor =
            CreateSut(
                invoker,
                stateUpdater: stateUpdater);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest());

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            StepExecutionStatus.Exception,
            outcome.Status);

        Assert.Equal(
            ExecutionDecisionType.ProcessViolation,
            outcome.Decision);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == StepProcessingMessageCode.FrameworkException);
    }

    private static Mock<IProcessorContextStore> CreateStore(
        List<ProcessorContext> savedContexts)
    {
        var store =
            new Mock<IProcessorContextStore>();

        store
            .Setup(x =>
                x.SaveAsync(
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<CancellationToken>()))
            .Callback<ProcessorContext, CancellationToken>(
                (saved, _) => savedContexts.Add(saved))
            .Returns(Task.CompletedTask);

        return store;
    }

    private static Mock<IStepAvailabilityResolver> CreateAvailabilityResolver()
    {
        var resolver =
            new Mock<IStepAvailabilityResolver>();

        resolver
            .Setup(x =>
                x.Resolve(
                    It.IsAny<StepCandidate>(),
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>()))
            .Returns([]);

        return resolver;
    }

    private static Mock<IStepExecutionEvaluator> CreateEvaluator()
    {
        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        evaluator
            .Setup(x =>
                x.Evaluate(
                    It.IsAny<StepCandidate>(),
                    It.IsAny<StepInvocationResult>(),
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>()))
            .Returns(ExecutionDecision.Complete());

        return evaluator;
    }

    private static Mock<IProcessorStateUpdater> CreateStateUpdater()
    {
        var updater =
            new Mock<IProcessorStateUpdater>();

        updater
            .Setup(x =>
                x.ApplyExecution(
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<StepCandidate>(),
                    It.IsAny<ExecutionDecision>()))
            .Returns((
                ProcessorContext context,
                StepCandidate _,
                ExecutionDecision _) =>
                context);

        updater
            .Setup(x =>
                x.ApplyException(
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<StepCandidate>()))
            .Returns((
                ProcessorContext context,
                StepCandidate _) =>
                context with { State = ProcessExecutionState.Exception });

        updater
            .Setup(x =>
                x.ApplyCancellation(
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<StepCandidate>()))
            .Returns((
                ProcessorContext context,
                StepCandidate _) =>
                context with { State = ProcessExecutionState.Canceled });

        return updater;
    }

    private static ProcessorExecutor CreateSut(
        Mock<IProcessStepInvoker> invoker,
        Mock<IStepAvailabilityResolver>? availabilityResolver = null,
        Mock<IStepExecutionEvaluator>? evaluator = null,
        Mock<IProcessorStateUpdater>? stateUpdater = null,
        Mock<IProcessorContextStore>? store = null,
        Mock<IProcessorEventFactory>? eventFactory = null,
        Mock<IEventPublisher>? eventPublisher = null,
        Mock<IKaleidoCorrelationContextAccessor>? correlationAccessor = null)
    {
        var observability =
            Mock.Of<IProcessorObservability>(
                x =>
                    x.BeginStep(It.IsAny<ProcessorStepObservationDetails>()) ==
                    Mock.Of<IProcessorStepObservation>());

        var accessor =
            correlationAccessor ??
            CreateCorrelationAccessor();

        return new ProcessorExecutor(
            invoker.Object,
            (evaluator ?? CreateEvaluator()).Object,
            (stateUpdater ?? CreateStateUpdater()).Object,
            (store ?? CreateStore([])).Object,
            (availabilityResolver ?? CreateAvailabilityResolver()).Object,
            (eventFactory ?? new Mock<IProcessorEventFactory>()).Object,
            (eventPublisher ?? new Mock<IEventPublisher>()).Object,
            observability,
            accessor.Object,
            NullLogger<ProcessorExecutor>.Instance);
    }

    private static Mock<IKaleidoCorrelationContextAccessor> CreateCorrelationAccessor()
    {
        var accessor =
            new Mock<IKaleidoCorrelationContextAccessor>();

        accessor
            .SetupGet(x => x.Current)
            .Returns(
                new KaleidoCorrelationContext
                {
                    RequestId = "test-request"
                });

        return accessor;
    }

    private static StepInvocationResult CreateInvokerResult(
        object? response = null)
    {
        return new StepInvocationResult
        {
            Succeeded = true,

            Response =
                response ?? new TestStepResponse(),

            Messages =
                []
        };
    }

    private static StepCandidate CreateCandidate<TStep>(
        string stepName)
        where TStep : new()
    {
        return CreateCandidate(
            stepName,
            CreateRegistration<TStep>(stepName),
            new TStep());
    }

    private static StepCandidate CreateCandidate<TStep>(
        string stepName,
        ProcessStepRegistration registration)
        where TStep : new()
    {
        return CreateCandidate(
            stepName,
            registration,
            new TStep());
    }

    private static StepCandidate CreateCandidate(
        string stepName,
        ProcessStepRegistration? registration,
        object? step)
    {
        return new StepCandidate
        {
            StepName =
                stepName,

            Registration =
                registration,

            Step =
                step,

            Status =
                StepCandidateStatus.Built
        };
    }

    private static ProcessStepRegistration CreateRegistration<TStep>(
        string name)
    {
        return new ProcessStepRegistration(
            typeof(TStep),
            typeof(TestStepResponse),
            typeof(TestStepHandler),
            [],
            [],
            [],
            new RepeatableOptions(),
            new ProcessStepMetadata(
                name,
                $"{name} description",
                "1.0.0",
                $"{name} displayname"));
    }

    private static ProcessorContext CreateContext(
        params string[] stepNames)
    {
        return new ProcessorContext
        {
            ProcessId =
                Guid.NewGuid(),

            ProcessorName =
                "test-processor",

            State =
                ProcessExecutionState.Active,

            AvailableSteps = [],

            CreatedUtc =
                DateTimeOffset.UtcNow,

            UpdatedUtc =
                DateTimeOffset.UtcNow,

            Steps =
                stepNames
                    .Select(x =>
                        new StepContext
                        {
                            StepName =
                                x,

                            Version =
                                "1.0.0",

                            Status =
                                StepExecutionStatus.Pending
                        })
                    .ToArray()
        };
    }

    private sealed class TestStepA
    {
    }

    private sealed class TestStepB
    {
    }

    private sealed class TestStepResponse
    {
    }

    private sealed class TestStepHandler
    {
    }
}
