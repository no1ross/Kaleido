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
                    Mock.Of<IExecutionProcessor>()));

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
                    Mock.Of<IExecutionProcessor>()));

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
                    Mock.Of<IExecutionProcessor>()));

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
                    null!));

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
    public async Task ExecuteAsync_WhenContextDoesNotExist_InitializesContext()
    {
        var request =
            CreateRequest();

        var initializedContext =
            CreateContext(
                request.ParticipantProcessId.Value,
                request.RequestId);

        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    request.ParticipantProcessId.Value,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParticipantContext?)null);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Initialize(
                    request.ParticipantProcessId.Value))
            .Returns(CreateContext(
                request.ParticipantProcessId.Value,
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
                CreateExecutionResult());

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object);

        await runtime.ExecuteAsync(request);

        stateUpdater.Verify(
            x =>
                x.Initialize(
                    request.ParticipantProcessId.Value),
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

        var existingContext =
            CreateContext(
                request.ParticipantProcessId.Value,
                "old-request");

        var reconciledContext =
            CreateContext(
                request.ParticipantProcessId.Value,
                request.RequestId);

        var contextStore =
            new Mock<IProcessContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    request.ParticipantProcessId.Value,
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
                CreateExecutionResult());

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object);

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

        var context =
            CreateContext(
                request.ParticipantProcessId.Value,
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
                    request.ParticipantProcessId.Value,
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
                CreateExecutionResult());

        var runtime =
            new ParticipantRuntime(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object);

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

        var context =
            CreateContext(
                request.ParticipantProcessId.Value,
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
                ParticipantProcessId = request.ParticipantProcessId.Value,
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
                    request.ParticipantProcessId.Value,
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
                processor.Object);

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

        var context =
            CreateContext(
                request.ParticipantProcessId.Value,
                request.RequestId);

        var plan =
            new ExecutionPlanResult { Candidates = [] };

        var executionResult =
            CreateExecutionResult();

        var sequence =
            new MockSequence();

        var contextStore =
            new Mock<IProcessContextStore>(MockBehavior.Strict);

        contextStore
            .InSequence(sequence)
            .Setup(x =>
                x.LoadAsync(
                    request.ParticipantProcessId.Value,
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
                processor.Object);

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
            Mock.Of<IExecutionProcessor>());
    }

    private static ProcessRequest CreateRequest()
    {
        return new ProcessRequest
        {
            ParticipantProcessId = Guid.NewGuid(),
            RequestId = "REQ-001",
            Participant = new ParticipantRequest()
        };
    }

    private static ParticipantContext CreateContext(
        Guid participantProcessId,
        string requestId)
    {
        return new ParticipantContext
        {
            ParticipantProcessId = participantProcessId,
            LatestRequestId = requestId
        };
    }

    private static ProcessExecutionResult CreateExecutionResult()
    {
        return new ProcessExecutionResult
        {
            ParticipantProcessId = Guid.NewGuid(),
            State = ProcessExecutionState.Active
        };
    }

    private sealed class TestResponse;
}