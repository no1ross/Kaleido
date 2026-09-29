using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;
using Kaleido.Process.Registry;
using Microsoft.Extensions.Logging.Abstractions;

using Kaleido.UnitTests;

namespace Kaleido.Process.UnitTests.Processor.Execution;

public sealed class ExecutionProcessorTests
    : SutFixture
{
    private static readonly KaleidoServiceOptions ServiceOptions =
        new() { ServiceName = "test-processor" };

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
        var registrationA =
            CreateRegistration<TestStepA>("step-a");

        var registrationB =
            CreateRegistration<TestStepB>("step-b");

        var candidate =
            CreateCandidate<TestStepA>("step-a", registrationA);

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

        var registry =
            CreateRegistry(registrationA, registrationB);

        var processor =
            CreateSut(invoker, registry);

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

        var registry =
            CreateRegistry(registration);

        var processor =
            CreateSut(invoker, registry);

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

        var registry =
            CreateRegistry(registration);

        var processor =
            CreateSut(invoker, registry);

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

        var registry =
            CreateRegistry(registration);

        var store =
            CreateStore();

        var processor =
            CreateSut(invoker, registry, store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                new ProcessorRequest());

        var persisted =
            await store.LoadAsync(context.ProcessId);

        Assert.NotNull(persisted);

        Assert.Equal(
            ProcessExecutionState.Complete,
            persisted.State);

        Assert.Equal(
            result.State,
            persisted.State);

        Assert.Null(persisted.RequiredStep);

        var step =
            persisted.FindStep("step-a");

        Assert.NotNull(step);

        Assert.Equal(
            StepExecutionStatus.Completed,
            step.Status);
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

        var registry =
            CreateRegistry(registration);

        var processor =
            CreateSut(invoker, registry);

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

        var registry =
            CreateRegistry(registrationA, registrationB);

        var store =
            CreateStore();

        var processor =
            CreateSut(invoker, registry, store);

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
            await store.LoadAsync(context.ProcessId);

        Assert.NotNull(persisted);

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

        var registry =
            CreateRegistry(registration);

        var store =
            CreateStore();

        var processor =
            CreateSut(invoker, registry, store);

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
            await store.LoadAsync(context.ProcessId);

        Assert.NotNull(persisted);

        Assert.Equal(
            ProcessExecutionState.Exception,
            persisted.State);

        var step =
            persisted.FindStep("step-a");

        Assert.NotNull(step);

        Assert.Equal(
            StepExecutionStatus.Exception,
            step.Status);
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

        var registry =
            CreateRegistry(registration);

        var store =
            CreateStore();

        var processor =
            CreateSut(invoker, registry, store);

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
            await store.LoadAsync(context.ProcessId);

        Assert.NotNull(persisted);

        Assert.Equal(
            ProcessExecutionState.Canceled,
            persisted.State);

        var step =
            persisted.FindStep("step-a");

        Assert.NotNull(step);

        Assert.Equal(
            StepExecutionStatus.Canceled,
            step.Status);
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

        var store =
            CreateStore();

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
            await store.LoadAsync(context.ProcessId);

        Assert.NotNull(persisted);

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

        var store =
            CreateStore();

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
            await store.LoadAsync(context.ProcessId);

        Assert.NotNull(persisted);

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
            new Mock<IProcessStateUpdater>();

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

    private static ProcessContextStore CreateStore() =>
        new(NullLogger<ProcessContextStore>.Instance);

    private static Mock<IProcessStepRegistry> CreateRegistry(
        params ProcessStepRegistration[] registrations)
    {
        var registry =
            new Mock<IProcessStepRegistry>();

        registry
            .SetupGet(x => x.Registrations)
            .Returns(registrations);

        return registry;
    }

    private static ExecutionProcessor CreateSut(
        Mock<IProcessStepInvoker> invoker,
        Mock<IProcessStepRegistry>? registry = null,
        ProcessContextStore? store = null,
        Mock<IProcessStateUpdater>? stateUpdater = null)
    {
        var resolvedRegistry =
            registry ?? CreateRegistry();

        var resolver =
            new StepAvailabilityResolver(resolvedRegistry.Object);

        var evaluator =
            new StepExecutionEvaluator(resolver, ServiceOptions);

        var resolvedStateUpdater =
            stateUpdater is not null
                ? (IProcessStateUpdater)stateUpdater.Object
                : new ProcessStateUpdater(resolvedRegistry.Object, ServiceOptions);

        var stateRepository =
            (IProcessContextStore)(store ?? CreateStore());

        var accessor =
            new KaleidoCorrelationContextAccessor();

        accessor.Initialize(
            new KaleidoCorrelationContext
            {
                RequestId = "test-request"
            });

        // Observability is a collaborator, not the SUT - mock it. A real
        // ProcessObservability would publish to the process-global Meter and
        // interfere with ProcessObservabilityTests' MeterListener.
        var observability =
            Mock.Of<IProcessObservability>(
                x =>
                    x.BeginStep(It.IsAny<ProcessStepObservationDetails>()) ==
                    Mock.Of<IProcessStepObservation>());

        var eventFactory =
            new ProcessEventFactory(ServiceOptions);

        var eventPublisher =
            new EventPublisher(NullLogger<EventPublisher>.Instance);

        return new ExecutionProcessor(
            invoker.Object,
            evaluator,
            resolvedStateUpdater,
            stateRepository,
            resolver,
            eventFactory,
            eventPublisher,
            observability,
            accessor,
            NullLogger<ExecutionProcessor>.Instance);
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
