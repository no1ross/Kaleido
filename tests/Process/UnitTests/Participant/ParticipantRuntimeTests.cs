using Kaleido.Eventing;
using Kaleido.Process;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Moq;
using Xunit;

namespace Kaleido.Process.UnitTests.Participant;

public sealed class ParticipantRuntimeTests
{
    [Fact]
    public void Constructor_WhenContextStoreIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ParticipantRuntime(
                    null!,
                    Mock.Of<IProcessStateUpdater>(),
                    Mock.Of<IExecutionPlanner>(),
                    Mock.Of<IExecutionProcessor>(),
                    Mock.Of<IProcessEventFactory>(),
                    CreateEventPublisher().Object,
                    CreateObservability().Object));

        Assert.Equal("contextStore", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenStateUpdaterIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ParticipantRuntime(
                    Mock.Of<IProcessContextStore>(),
                    null!,
                    Mock.Of<IExecutionPlanner>(),
                    Mock.Of<IExecutionProcessor>(),
                    Mock.Of<IProcessEventFactory>(),
                    CreateEventPublisher().Object,
                    CreateObservability().Object));

        Assert.Equal("stateUpdater", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenPlannerIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ParticipantRuntime(
                    Mock.Of<IProcessContextStore>(),
                    Mock.Of<IProcessStateUpdater>(),
                    null!,
                    Mock.Of<IExecutionProcessor>(),
                    Mock.Of<IProcessEventFactory>(),
                    CreateEventPublisher().Object,
                    CreateObservability().Object));

        Assert.Equal("planner", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenProcessorIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ParticipantRuntime(
                    Mock.Of<IProcessContextStore>(),
                    Mock.Of<IProcessStateUpdater>(),
                    Mock.Of<IExecutionPlanner>(),
                    null!,
                    Mock.Of<IProcessEventFactory>(),
                    CreateEventPublisher().Object,
                    CreateObservability().Object));

        Assert.Equal("processor", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestIsNull_Throws()
    {
        var runtime =
            CreateRuntime();

        var exception =
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                runtime.ExecuteAsync(null!));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInitialRequestContainsMultipleStepsWithoutProcessId_InitializesContextAndExecutes()
    {
        var request =
            new ProcessRequest
            {
                ProcessId = null,
                RequestId = "REQ-INITIAL-MULTI",
                Participant = new ParticipantRequest
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
                request.RequestId);

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
                    request.Participant,
                    It.IsAny<ParticipantContext>()))
            .Returns<ParticipantRequest, ParticipantContext>((participant, context) =>
            {
                Assert.Equal(request.RequestId, context.LatestRequestId);
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
                    It.IsAny<ParticipantContext>(),
                    It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyCollection<StepCandidate>, ParticipantContext, CancellationToken>((_, context, _) =>
                Task.FromResult(
                    CreateExecutionResult(
                        context.ProcessId)));

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object);

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
                request.RequestId);

        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParticipantContext?)null);

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
                    request.Participant,
                    It.IsAny<ParticipantContext>()))
            .Returns(new ExecutionPlanResult{ Candidates = [] });

        var processor =
            new Mock<IExecutionProcessor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ParticipantContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object);

        await runtime.ExecuteAsync(request);

        stateUpdater.Verify(
            x =>
                x.Initialize(
                    request.ProcessId.Value),
            Times.Once);

        stateUpdater.Verify(
            x =>
                x.Reconcile(
                    It.IsAny<ParticipantContext>()),
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
                request.RequestId);

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
                    request.Participant,
                    reconciledContext))
            .Returns(new ExecutionPlanResult { Candidates = [] });

        var processor =
            new Mock<IExecutionProcessor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    reconciledContext,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object);

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
                request.RequestId);

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
                    request.Participant,
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
                    It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StepCandidate>, ParticipantContext, CancellationToken>(
                (candidates, _, _) => capturedCandidates = candidates)
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object);

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
                request.RequestId);

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
                    request.Participant,
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
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object);

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
        Assert.Equal(ExecutionDecisionType.Complete, step.Decision);

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
                request.RequestId);

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
                    request.Participant,
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
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                CreateEventPublisher().Object,
                CreateObservability().Object);

        await runtime.ExecuteAsync(request);

        contextStore.VerifyAll();
        stateUpdater.VerifyAll();
        planner.VerifyAll();
        processor.VerifyAll();
    }

    private static ParticipantRuntime CreateRuntime()
    {
        return new ParticipantRuntime(
            Mock.Of<IProcessContextStore>(),
            Mock.Of<IProcessStateUpdater>(),
            Mock.Of<IExecutionPlanner>(),
            Mock.Of<IExecutionProcessor>(),
            CreateProcessEventFactory().Object,
            CreateEventPublisher().Object,
            CreateObservability().Object);
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
                    It.IsAny<ProcessCreated>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<PlanBuilt>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ExecutionCompleted>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return publisher;
    }

    private static Mock<IProcessEventFactory> CreateProcessEventFactory()
    {
        var factory =
            new Mock<IProcessEventFactory>(MockBehavior.Strict);

        factory
            .Setup(x =>
                x.CreateProcessCreated(
                    It.IsAny<ParticipantContext>(),
                    It.IsAny<ProcessRequest>()))
            .Returns<ParticipantContext, ProcessRequest>((context, request) =>
            {
                var participant =
                    request.Participant ?? new ParticipantRequest();

                return new Eventing.ProcessCreated
                {
                    ProcessId = context.ProcessId,
                    OccurredOn = DateTimeOffset.UtcNow,
                    State = context.State,
                    CreatedUtc = context.CreatedUtc,
                    UpdatedUtc = context.UpdatedUtc,
                    SubmittedStepNames = participant.Steps.Keys.ToArray(),
                    SubmittedStepCount = participant.Steps.Count
                };
            });

        factory
            .Setup(x =>
                x.CreatePlanBuilt(
                    It.IsAny<ParticipantContext>(),
                    It.IsAny<ProcessRequest>(),
                    It.IsAny<ExecutionPlanResult>(),
                    It.IsAny<int>()))
            .Returns<ParticipantContext, ProcessRequest, ExecutionPlanResult, int>((context, request, plan, executableCount) =>
            {
                var participant =
                    request.Participant ?? new ParticipantRequest();

                return new Eventing.PlanBuilt
                {
                    ProcessId = context.ProcessId,
                    OccurredOn = DateTimeOffset.UtcNow,
                    State = context.State,
                    RequiredStep = context.RequiredStep,
                    AvailableSteps = context.AvailableSteps,
                    SubmittedStepNames = participant.Steps.Keys.ToArray(),
                    SubmittedStepCount = participant.Steps.Count,
                    CandidateCount = plan.Candidates.Count,
                    ExecutableCount = executableCount,
                    Candidates = []
                };
            });

        factory
            .Setup(x =>
                x.CreateExecutionCompleted(
                    It.IsAny<ProcessExecutionResult>()))
            .Returns<ProcessExecutionResult>(executionResult =>
                new Eventing.ExecutionCompleted
                {
                    ProcessId = executionResult.ProcessId,
                    OccurredOn = DateTimeOffset.UtcNow,
                    State = executionResult.State,
                    RequiredStep = executionResult.RequiredStep,
                    AvailableSteps = executionResult.AvailableSteps,
                    ExecutedStepCount = executionResult.Outcomes.Count
                });

        return factory;
    }

    private static ProcessRequest CreateRequest()
    {
        return new ProcessRequest
        {
            ProcessId = Guid.NewGuid(),
            RequestId = "REQ-001",
            Participant = new ParticipantRequest()
        };
    }

    private static ParticipantContext CreateContext(
        Guid processId,
        string requestId)
    {
        return new ParticipantContext
        {
            ProcessId = processId,
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