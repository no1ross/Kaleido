using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;

namespace Kaleido.Process.UnitTests.Processor;

public sealed class ProcessRuntimeTests
{
    [Fact]
    public async Task ExecuteAsync_WhenInitialRequestContainsMultipleStepsWithoutProcessId_InitializesContextAndExecutes()
    {
        var request =
            new ProcessRequest
            {
                ProcessId = null,
                Processor = new ProcessorRequest
                {
                    Steps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["step-a"] = new { Value = 1 },
                        ["step-b"] = new { Value = 2 }
                    }
                }
            };

        var initializedContext =
            CreateContext(
                Guid.NewGuid(),
                "REQ-INITIAL-MULTI");

        var contextStore =
            new Mock<IProcessContextStore>(MockBehavior.Strict);

        var stateUpdater =
            new Mock<IProcessStateUpdater>(MockBehavior.Strict);

        stateUpdater
            .Setup(x =>
                x.Initialize(
                    It.IsAny<Guid>()))
            .Returns<Guid>(processId =>
                initializedContext with
                {
                    ProcessId = processId
                });

        var planner =
            new Mock<IExecutionPlanner>(MockBehavior.Strict);

        planner
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    It.IsAny<ProcessorContext>()))
            .Returns<ProcessorRequest, ProcessorContext>((processor, context) =>
            {
                Assert.Equal("REQ-INITIAL-MULTI", context.LatestRequestId);
                Assert.NotEqual(Guid.Empty, context.ProcessId);

                return new ExecutionPlanResult
                {
                    Candidates = []
                };
            });

        var processor =
            new Mock<IExecutionProcessor>(MockBehavior.Strict);

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<ProcessorRequest>(),
                    It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyCollection<StepCandidate>, ProcessorContext, ProcessorRequest, CancellationToken>((_, context, _, _) =>
                Task.FromResult(
                    CreateExecutionResult(
                        context.ProcessId)));

        var runtime =
            new ProcessRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor("REQ-INITIAL-MULTI"));

        var result =
            await runtime.ExecuteAsync(request);

        Assert.NotEqual(Guid.Empty, result.ProcessId);

        contextStore.Verify(
            x =>
                x.LoadAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        stateUpdater.Verify(
            x =>
                x.Initialize(
                    It.IsAny<Guid>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextDoesNotExist_InitializesContext()
    {
        var request =
            CreateRequest();

        var processId =
            Assert.IsType<Guid>(
                request.ProcessId);

        var initializedContext =
            CreateContext(
                processId,
                "REQ-001");

        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessorContext?)null);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Initialize(
                    request.ProcessId.Value))
            .Returns(CreateContext(
                request.ProcessId.Value,
                "ignored"));

        var planner =
            new Mock<IExecutionPlanner>();

        planner
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    It.IsAny<ProcessorContext>()))
            .Returns(new ExecutionPlanResult { Candidates = [] });

        var processor =
            new Mock<IExecutionProcessor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<ProcessorRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            new ProcessRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor());

        await runtime.ExecuteAsync(request);

        stateUpdater.Verify(
            x =>
                x.Initialize(
                    request.ProcessId.Value),
            Times.Once);

        stateUpdater.Verify(
            x =>
                x.Reconcile(
                    It.IsAny<ProcessorContext>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextExists_ReconcilesContext()
    {
        var request =
            CreateRequest();

        var processId =
            Assert.IsType<Guid>(
                request.ProcessId);

        var existingContext =
            CreateContext(
                processId,
                "old-request");

        var reconciledContext =
            CreateContext(
                processId,
                "REQ-001");

        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContext);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Reconcile(existingContext))
            .Returns(reconciledContext);

        var planner =
            new Mock<IExecutionPlanner>();

        planner
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    reconciledContext))
            .Returns(new ExecutionPlanResult { Candidates = [] });

        var processor =
            new Mock<IExecutionProcessor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    reconciledContext,
                    It.IsAny<ProcessorRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            new ProcessRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor());

        await runtime.ExecuteAsync(request);

        stateUpdater.Verify(
            x =>
                x.Reconcile(existingContext),
            Times.Once);

        stateUpdater.Verify(
            x =>
                x.Initialize(
                    It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PassesOnlyExecutionCandidatesToProcessor()
    {
        var request =
            CreateRequest();

        var processId =
            Assert.IsType<Guid>(
                request.ProcessId);

        var context =
            CreateContext(
                processId,
                "REQ-001");

        var executableCandidate =
            new StepCandidate
            {
                StepName = "step-a",
                IncludedInExecutionPlan = true
            };

        var excludedCandidate =
            new StepCandidate
            {
                StepName = "step-b",
                IncludedInExecutionPlan = false
            };

        IReadOnlyCollection<StepCandidate>? capturedCandidates =
            null;

        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Reconcile(context))
            .Returns(context);

        var planner =
            new Mock<IExecutionPlanner>();

        planner
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    context))
            .Returns(
                new ExecutionPlanResult
                {
                    Candidates =
                    [
                        executableCandidate,
                        excludedCandidate
                    ]
                });

        var processor =
            new Mock<IExecutionProcessor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context,
                    It.IsAny<ProcessorRequest>(),
                    It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StepCandidate>, ProcessorContext, ProcessorRequest, CancellationToken>(
                (candidates, _, _, _) => capturedCandidates = candidates)
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            new ProcessRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor());

        await runtime.ExecuteAsync(request);

        Assert.NotNull(capturedCandidates);

        var executionCandidate =
            Assert.Single(capturedCandidates);

        Assert.Equal(
            "step-a",
            executionCandidate.StepName);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsMergedResult()
    {
        var request =
            CreateRequest();

        var processId =
            Assert.IsType<Guid>(
                request.ProcessId);

        var context =
            CreateContext(
                processId,
                "REQ-001");

        var candidate =
            new StepCandidate
            {
                StepName = "step-a",
                Status = StepCandidateStatus.Built,
                IncludedInExecutionPlan = true
            };

        candidate.AddError(
            StepProcessingMessageCode.InvalidRequest,
            "candidate-message");

        var executionOutcome =
            new ProcessExecutionOutcome
            {
                StepName = "step-a",
                Status = StepExecutionStatus.Completed,
                Outcome = StepExecutionOutcome.Completed,
                Decision = ExecutionDecisionType.Complete,
                Response =
                    new TestResponse(),
                RuntimeMessages =
                [
                    StepProcessingMessage.Information(
                        StepProcessingMessageCode.ProcessMessage,
                        "execution-message")
                ]
            };

        var executionResult =
            new ProcessExecutionResult
            {
                ProcessId = processId,
                State = ProcessExecutionState.Complete,
                Outcomes =
                [
                    executionOutcome
                ]
            };

        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Reconcile(context))
            .Returns(context);

        var planner =
            new Mock<IExecutionPlanner>();

        planner
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    context))
            .Returns(
                new ExecutionPlanResult
                {
                    Candidates = [candidate]
                });

        var processor =
            new Mock<IExecutionProcessor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context,
                    It.IsAny<ProcessorRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);

        var runtime =
            new ProcessRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor());

        var result =
            await runtime.ExecuteAsync(request);

        Assert.Equal(
            ProcessExecutionState.Complete,
            result.State);

        var step =
            Assert.Single(result.Steps);

        Assert.Equal("step-a", step.StepName);
        Assert.Equal(StepCandidateStatus.Built, step.CandidateStatus);
        Assert.Equal(StepExecutionStatus.Completed, step.ExecutionStatus);

        Assert.Equal(
            2,
            step.RuntimeMessages.Count);
    }

    [Fact]
    public async Task ExecuteAsync_CallsCollaboratorsInOrder()
    {
        var request =
            CreateRequest();

        var processId =
            Assert.IsType<Guid>(
                request.ProcessId);

        var context =
            CreateContext(
                processId,
                "REQ-001");

        var plan =
            new ExecutionPlanResult { Candidates = [] };

        var executionResult =
            CreateExecutionResult(processId);

        var sequence =
            new MockSequence();

        var contextStore =
            new Mock<IProcessContextStore>(MockBehavior.Strict);

        contextStore
            .InSequence(sequence)
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var stateUpdater =
            new Mock<IProcessStateUpdater>(MockBehavior.Strict);

        stateUpdater
            .InSequence(sequence)
            .Setup(x =>
                x.Reconcile(context))
            .Returns(context);

        var planner =
            new Mock<IExecutionPlanner>(MockBehavior.Strict);

        planner
            .InSequence(sequence)
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    context))
            .Returns(plan);

        var processor =
            new Mock<IExecutionProcessor>(MockBehavior.Strict);

        processor
            .InSequence(sequence)
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context,
                    It.IsAny<ProcessorRequest>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);

        var runtime =
            new ProcessRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor());

        await runtime.ExecuteAsync(request);

        contextStore.VerifyAll();
        stateUpdater.VerifyAll();
        planner.VerifyAll();
        processor.VerifyAll();
    }

    private static Mock<IProcessObservability> CreateObservability()
    {
        var executionObservation =
            new Mock<IProcessExecutionObservation>();

        executionObservation
            .Setup(x => x.Dispose());

        var observability =
            new Mock<IProcessObservability>();

        observability
            .Setup(x =>
                x.BeginExecution(
                    It.IsAny<ProcessExecutionObservationDetails>()))
            .Returns(executionObservation.Object);

        return observability;
    }

    private static Mock<IEventPublisher> CreateEventPublisher()
    {
        var publisher =
            new Mock<IEventPublisher>(MockBehavior.Strict);

        publisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<KaleidoEventEnvelope<PlanBuilt, ProcessEventContext>>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<KaleidoEventEnvelope<ExecutionCompleted, ProcessEventContext>>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return publisher;
    }

    private static ProcessEventContext CreateStubContext(Guid processId) =>
        new()
        {
            RequestId = Guid.NewGuid().ToString(),
            ServiceName = "test",
            ProcessId = processId,
            StepName = string.Empty
        };

    private static Mock<IProcessEventFactory> CreateProcessEventFactory()
    {
        var factory =
            new Mock<IProcessEventFactory>(MockBehavior.Strict);

        factory
            .Setup(x =>
                x.CreateProcessCreated(
                    It.IsAny<KaleidoCorrelationContext>(),
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<ProcessRequest>()))
            .Returns<KaleidoCorrelationContext, ProcessorContext, ProcessRequest>((_, context, request) =>
            {
                var processor = request.Processor ?? new ProcessorRequest();
                return new KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>
                {
                    EventType = "process.created.v1",
                    Context = CreateStubContext(context.ProcessId),
                    Event = new ProcessCreated
                    {
                        OccurredOn = DateTimeOffset.UtcNow,
                        State = context.State,
                        CreatedUtc = context.CreatedUtc,
                        UpdatedUtc = context.UpdatedUtc,
                        SubmittedStepNames = processor.Steps.Keys.ToArray(),
                        SubmittedStepCount = processor.Steps.Count
                    }
                };
            });

        factory
            .Setup(x =>
                x.CreatePlanBuilt(
                    It.IsAny<KaleidoCorrelationContext>(),
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<ProcessRequest>(),
                    It.IsAny<ExecutionPlanResult>(),
                    It.IsAny<int>()))
            .Returns<KaleidoCorrelationContext, ProcessorContext, ProcessRequest, ExecutionPlanResult, int>((_, context, request, plan, executableCount) =>
            {
                var processor = request.Processor ?? new ProcessorRequest();
                return new KaleidoEventEnvelope<PlanBuilt, ProcessEventContext>
                {
                    EventType = "process.plan-built.v1",
                    Context = CreateStubContext(context.ProcessId),
                    Event = new PlanBuilt
                    {
                        OccurredOn = DateTimeOffset.UtcNow,
                        State = context.State,
                        RequiredStep = context.RequiredStep,
                        AvailableSteps = context.AvailableSteps,
                        SubmittedStepNames = processor.Steps.Keys.ToArray(),
                        SubmittedStepCount = processor.Steps.Count,
                        CandidateCount = plan.Candidates.Count,
                        ExecutableCount = executableCount,
                        Candidates = []
                    }
                };
            });

        factory
            .Setup(x =>
                x.CreateExecutionCompleted(
                    It.IsAny<KaleidoCorrelationContext>(),
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<ProcessExecutionResult>()))
            .Returns<KaleidoCorrelationContext, ProcessorContext, ProcessExecutionResult>((_, context, executionResult) =>
                new KaleidoEventEnvelope<ExecutionCompleted, ProcessEventContext>
                {
                    EventType = "process.execution-completed.v1",
                    Context = CreateStubContext(executionResult.ProcessId),
                    Event = new ExecutionCompleted
                    {
                        OccurredOn = DateTimeOffset.UtcNow,
                        State = executionResult.State,
                        RequiredStep = executionResult.RequiredStep,
                        AvailableSteps = executionResult.AvailableSteps,
                        ExecutedStepCount = executionResult.Outcomes.Count
                    }
                });

        return factory;
    }

    private static ProcessRequest CreateRequest()
    {
        return new ProcessRequest
        {
            ProcessId = Guid.NewGuid(),
            Processor = new ProcessorRequest()
        };
    }

    private static IKaleidoCorrelationContextAccessor CreateCorrelationAccessor(
        string requestId = "REQ-001")
    {
        var accessor = new Mock<IKaleidoCorrelationContextAccessor>();

        accessor
            .SetupGet(x => x.Current)
            .Returns(new KaleidoCorrelationContext
            {
                RequestId = requestId
            });

        return accessor.Object;
    }

    private static ProcessorContext CreateContext(
        Guid processId,
        string requestId)
    {
        return new ProcessorContext
        {
            ProcessId = processId,
            ProcessorName = "test-processor",
            LatestRequestId = requestId,
            State = ProcessExecutionState.Active,
            AvailableSteps = [],
            Steps = [],
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
    }

    private static ProcessExecutionResult CreateExecutionResult(
        Guid? processId = null)
    {
        return new ProcessExecutionResult
        {
            ProcessId = processId ?? Guid.NewGuid(),
            State = ProcessExecutionState.Active,
            AvailableSteps = [],
            Outcomes = []
        };
    }

    private sealed class TestResponse;
}