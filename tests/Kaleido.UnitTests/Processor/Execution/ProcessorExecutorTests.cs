using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;
using Kaleido.Processor.Registry;
using Kaleido.Storage;
using Kaleido.UnitTests;

namespace Kaleido.Processor.Execution.UnitTests;

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
                context, TestContext.Current.CancellationToken);

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
            context, TestContext.Current.CancellationToken);

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
            context, TestContext.Current.CancellationToken);

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
                context, TestContext.Current.CancellationToken);

        var outcome =
            Assert.Single(result.Outcomes);

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
            CreateTransition(savedContexts);

        var processor =
            CreateSut(
                invoker,
                stateUpdater: stateUpdater,
                transition: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context, TestContext.Current.CancellationToken);

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
    public async Task ExecuteAsync_WhenTheStepIsRejected_LeavesTheProcessUnchangedAndReturnsTheMessages()
    {
        var registration = CreateRegistration<TestStepA>("step-a");
        var candidate = CreateCandidate<TestStepA>("step-a", registration);
        var context = CreateContext("step-a") with { State = ProcessExecutionState.AwaitingStepSelection, AvailableSteps = ["step-a", "step-b"] };
        var message = new ProcessMessage { Code = "UNKNOWN_CODE", Type = MessageType.Error, Message = "No processor is configured for the code." };

        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessStepRegistration>(), It.IsAny<object>(), It.IsAny<ProcessStepContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepInvocationResult { Succeeded = false, Rejected = true, Messages = [message] });

        var evaluator = new Mock<IStepExecutionEvaluator>();
        evaluator
            .Setup(x => x.Evaluate(It.IsAny<StepCandidate>(), It.IsAny<StepInvocationResult>(), It.IsAny<IReadOnlyCollection<StepCandidate>>(), It.IsAny<ProcessorContext>()))
            .Returns(ExecutionDecision.Rejected());

        var stateUpdater = new Mock<IProcessorStateUpdater>(MockBehavior.Strict);
        var saved = new List<ProcessorContext>();

        var result = await CreateSut(invoker, evaluator: evaluator, stateUpdater: stateUpdater, transition: CreateTransition(saved))
            .ExecuteAsync([candidate], context, TestContext.Current.CancellationToken);

        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(StepExecutionStatus.Rejected, outcome.Status);
        Assert.Equal([message], outcome.BusinessMessages);
        Assert.Empty(saved);
        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
        Assert.Equal(["step-a", "step-b"], result.AvailableSteps);
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
                context, TestContext.Current.CancellationToken);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            "step-a",
            outcome.StepName);

        Assert.Equal(
            StepExecutionStatus.Completed,
            outcome.Status);

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
            CreateTransition(savedContexts);

        var processor =
            CreateSut(
                invoker,
                evaluator: evaluator,
                stateUpdater: stateUpdater,
                transition: store);

        var result =
            await processor.ExecuteAsync(
                [firstCandidate, nextCandidate],
                context, TestContext.Current.CancellationToken);

        Assert.Collection(
            result.Outcomes,
            first =>
            {
                Assert.Equal(
                    "step-a",
                    first.StepName);
            },
            second =>
            {
                Assert.Equal(
                    "step-b",
                    second.StepName);
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
    public async Task ExecuteAsync_WhenInvokerThrowsValidationException_PreservesItsCode()
    {
        var candidate = CreateCandidate<TestStepA>("step-a");
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(
                It.IsAny<ProcessStepRegistration>(),
                It.IsAny<object>(),
                It.IsAny<ProcessStepContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KaleidoValidationException("pro_custom_validation", "Member invalid."));
        var stepObservation = new Mock<IProcessorStepObservation>();
        var stateUpdater = CreateStateUpdater();

        var result = await CreateSut(invoker, stateUpdater: stateUpdater, stepObservation: stepObservation).ExecuteAsync(
            [candidate], CreateContext("step-a"), TestContext.Current.CancellationToken);

        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(StepExecutionStatus.ValidationFailed, outcome.Status);

        // The history records the same status as the answer (#536); it used to say Exception.
        stateUpdater.Verify(x => x.ApplyUnfinishedStep(It.IsAny<ProcessorContext>(), candidate, StepExecutionStatus.ValidationFailed), Times.Once);
        var message = Assert.Single(outcome.RuntimeMessages);
        Assert.Equal("pro_custom_validation", message.Code);
        Assert.Equal("Member invalid.", message.Message);
        stepObservation.Verify(x => x.StepValidationFailed("pro_custom_validation"), Times.Once);
        stepObservation.Verify(x => x.StepFailed(It.IsAny<Exception>()), Times.Never);
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
            CreateTransition(savedContexts);

        var processor =
            CreateSut(invoker, transition: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context, TestContext.Current.CancellationToken);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            "step-a",
            outcome.StepName);

        Assert.Equal(
            StepExecutionStatus.Exception,
            outcome.Status);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == ProcessorErrorCodes.FrameworkException);

        var persisted =
            Assert.Single(savedContexts);

        Assert.Equal(
            context.State,
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
            CreateTransition(savedContexts);

        var processor =
            CreateSut(invoker, transition: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                cancellationTokenSource.Token);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            "step-a",
            outcome.StepName);

        Assert.Equal(
            StepExecutionStatus.Canceled,
            outcome.Status);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == ProcessorErrorCodes.ExecutionCanceled);

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
            context.State,
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
            CreateTransition(savedContexts);

        var processor =
            CreateSut(invoker, transition: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context, TestContext.Current.CancellationToken);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            StepExecutionStatus.Exception,
            outcome.Status);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == ProcessorErrorCodes.FrameworkException);

        var persisted =
            Assert.Single(savedContexts);

        Assert.Equal(
            context.State,
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
            CreateTransition(savedContexts);

        var processor =
            CreateSut(invoker, transition: store);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context, TestContext.Current.CancellationToken);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            StepExecutionStatus.Exception,
            outcome.Status);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == ProcessorErrorCodes.FrameworkException);

        var persisted =
            Assert.Single(savedContexts);

        Assert.Equal(
            context.State,
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
                x.ApplyUnfinishedStep(
                    context,
                    candidate, StepExecutionStatus.Exception))
            .Returns(context with { State = ProcessExecutionState.AwaitingRequiredStep });

        var processor =
            CreateSut(
                invoker,
                stateUpdater: stateUpdater);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context, TestContext.Current.CancellationToken);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(
            StepExecutionStatus.Exception,
            outcome.Status);

        Assert.Contains(
            outcome.RuntimeMessages,
            x => x.Code == ProcessorErrorCodes.FrameworkException);
    }

    private static async Task<ProcessExecutionOutcome> ExecuteOneAsync(
        StepCandidateStatus candidateStatus,
        bool succeeded,
        ExecutionDecision decision,
        Mock<IProcessorStepObservation>? stepObservation = null)
    {
        var candidate = CreateCandidate<TestStepA>("step-a");
        candidate.Status = candidateStatus;
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessStepRegistration>(), It.IsAny<object>(), It.IsAny<ProcessStepContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInvokerResult() with { Succeeded = succeeded });
        var evaluator = new Mock<IStepExecutionEvaluator>();
        evaluator
            .Setup(x => x.Evaluate(It.IsAny<StepCandidate>(), It.IsAny<StepInvocationResult>(), It.IsAny<IReadOnlyCollection<StepCandidate>>(), It.IsAny<ProcessorContext>()))
            .Returns(decision);

        var result =
            await CreateSut(invoker, evaluator: evaluator, stepObservation: stepObservation)
                .ExecuteAsync([candidate], CreateContext("step-a"), TestContext.Current.CancellationToken);

        return Assert.Single(result.Outcomes);
    }

    [Fact]
    public async Task ExecuteAsync_AProcessViolationAfterASuccessfulHandler_IsReportedAsAnException()
    {
        var violation = StepProcessingMessage.Error(ProcessorErrorCodes.FrameworkException, "not a valid next step");

        var outcome = await ExecuteOneAsync(StepCandidateStatus.Built, succeeded: true, ExecutionDecision.ProcessViolation(violation));

        Assert.Equal(StepExecutionStatus.Exception, outcome.Status);
        Assert.Equal([violation], outcome.RuntimeMessages);
    }

    [Fact]
    public async Task ExecuteAsync_ABusinessFailure_IsReportedAsFailed()
    {
        var outcome = await ExecuteOneAsync(StepCandidateStatus.Built, succeeded: false, ExecutionDecision.BusinessFailure());

        Assert.Equal(StepExecutionStatus.Failed, outcome.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheDecisionAsksForInformation_RecordsTheRequest()
    {
        var stepObservation = new Mock<IProcessorStepObservation>();
        var request = new InformationRequest { InformationRequestId = "out-of-network", Items = [] };

        var outcome = await ExecuteOneAsync(StepCandidateStatus.Built, succeeded: true, ExecutionDecision.AwaitingInformation("step-b", request), stepObservation);

        Assert.Equal(StepExecutionStatus.Completed, outcome.Status);
        stepObservation.Verify(x => x.DecisionRecorded(nameof(ExecutionDecisionType.AwaitingInformation), nameof(StepExecutionStatus.Completed)), Times.Once);
        stepObservation.Verify(x => x.InformationRequested("out-of-network"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheInvokerReportsAConcurrencyConflict_LetsItThroughWithoutSavingAFailure()
    {
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessStepRegistration>(), It.IsAny<object>(), It.IsAny<ProcessStepContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KaleidoConcurrencyException(Guid.NewGuid()));
        var saved = new List<ProcessorContext>();
        var stepObservation = new Mock<IProcessorStepObservation>();

        await Assert.ThrowsAsync<KaleidoConcurrencyException>(() =>
            CreateSut(invoker, transition: CreateTransition(saved), stepObservation: stepObservation)
                .ExecuteAsync([CreateCandidate<TestStepA>("step-a")], CreateContext("step-a"), TestContext.Current.CancellationToken));

        Assert.Empty(saved);
        stepObservation.Verify(x => x.StepFailed(It.IsAny<Exception>()), Times.Never);
    }

    private static Mock<IProcessorTransition> CreateTransition(
        List<ProcessorContext> savedContexts)
    {
        var store =
            new Mock<IProcessorTransition>();

        store
            .Setup(x =>
                x.SaveAsync(
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<KaleidoOutboxEvent?>(),
                    It.IsAny<CancellationToken>()))
            .Callback<ProcessorContext, KaleidoOutboxEvent?, CancellationToken>(
                (saved, _, _) => savedContexts.Add(saved))
            .ReturnsAsync((ProcessorContext saved, KaleidoOutboxEvent? _, CancellationToken _) => saved with { Version = saved.Version + 1 });

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

        // A step that did not finish leaves the process as it was; only its status is recorded.
        updater
            .Setup(x =>
                x.ApplyUnfinishedStep(
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<StepCandidate>(),
                    It.IsAny<StepExecutionStatus>()))
            .Returns((
                ProcessorContext context,
                StepCandidate _,
                StepExecutionStatus _) =>
                context);

        return updater;
    }

    private static ProcessorExecutor CreateSut(
        Mock<IProcessStepInvoker> invoker,
        Mock<IStepAvailabilityResolver>? availabilityResolver = null,
        Mock<IStepExecutionEvaluator>? evaluator = null,
        Mock<IProcessorStateUpdater>? stateUpdater = null,
        Mock<IProcessorTransition>? transition = null,
        Mock<IProcessorEventFactory>? eventFactory = null,
        Mock<IKaleidoEventWriter>? eventOutbox = null,
        Mock<IKaleidoCorrelationContextAccessor>? correlationAccessor = null,
        Mock<IProcessorStepObservation>? stepObservation = null)
    {
        var observedStep =
            stepObservation?.Object ?? Mock.Of<IProcessorStepObservation>();

        var observability =
            Mock.Of<IProcessorObservability>(
                x =>
                    x.BeginStep(It.IsAny<ProcessorStepObservationDetails>()) ==
                    observedStep);

        var accessor =
            correlationAccessor ??
            CreateCorrelationAccessor();

        return new ProcessorExecutor(
            invoker.Object,
            (evaluator ?? CreateEvaluator()).Object,
            (stateUpdater ?? CreateStateUpdater()).Object,
            (transition ?? CreateTransition([])).Object,
            (availabilityResolver ?? CreateAvailabilityResolver()).Object,
            (eventFactory ?? new Mock<IProcessorEventFactory>()).Object,
            (eventOutbox ?? new Mock<IKaleidoEventWriter>()).Object,
            observability,
            accessor.Object);
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
            new ProcessStepMetadata { Name = name, Description = $"{name} description", DisplayName = $"{name} displayname" });
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

    // A conflict is not a step failure: nothing was saved, so no failure state is saved over it and
    // the conflict reaches the caller (409).
    [Fact]
    public async Task ExecuteAsync_WhenTheSaveConflicts_LetsTheConflictThroughWithoutSavingAFailure()
    {
        var registration = CreateRegistration<TestStepA>("step-a");
        var candidate = CreateCandidate<TestStepA>("step-a", registration);
        var context = CreateContext("step-a");

        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(
                It.IsAny<ProcessStepRegistration>(),
                It.IsAny<object>(),
                It.IsAny<ProcessStepContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInvokerResult());

        var store = new Mock<IProcessorTransition>();
        store
            .Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KaleidoConcurrencyException(context.ProcessId));

        await Assert.ThrowsAsync<KaleidoConcurrencyException>(() =>
            CreateSut(invoker, transition: store).ExecuteAsync([candidate], context, TestContext.Current.CancellationToken));

        store.Verify(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SavesWithTheVersionTheContextWasLoadedWith()
    {
        var registration = CreateRegistration<TestStepA>("step-a");
        var candidate = CreateCandidate<TestStepA>("step-a", registration);
        var context = CreateContext("step-a");

        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(
                It.IsAny<ProcessStepRegistration>(),
                It.IsAny<object>(),
                It.IsAny<ProcessStepContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var saved = new List<ProcessorContext>();

        var result = await CreateSut(invoker, transition: CreateTransition(saved))
            .ExecuteAsync([candidate], context, TestContext.Current.CancellationToken);

        Assert.Equal(context.Version, Assert.Single(saved).Version);
        Assert.Equal(context.ProcessId, result.ProcessId);
    }

    private static Mock<IProcessStepInvoker> SucceedingInvoker()
    {
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(
                It.IsAny<ProcessStepRegistration>(),
                It.IsAny<object>(),
                It.IsAny<ProcessStepContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInvokerResult());

        return invoker;
    }

    private static KaleidoOutboxEvent OutboxEvent() =>
        new() { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessStepCompleted, Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow };

    private static Mock<IProcessorTransition> RecordingTransition(List<IReadOnlyCollection<KaleidoOutboxEvent>> savedEvents)
    {
        var store = new Mock<IProcessorTransition>();
        store
            .Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessorContext, KaleidoOutboxEvent?, CancellationToken>((_, transitionEvent, _) => savedEvents.Add(transitionEvent is null ? [] : [transitionEvent]))
            .ReturnsAsync((ProcessorContext saved, KaleidoOutboxEvent? _, CancellationToken _) => saved with { Version = saved.Version + 1 });

        return store;
    }

    [Fact]
    public async Task ExecuteAsync_ACompletedStep_SavesItsEventWithTheState()
    {
        var registration = CreateRegistration<TestStepA>("step-a");
        var candidate = CreateCandidate<TestStepA>("step-a", registration);
        var stepEvent = OutboxEvent();
        var outbox = new Mock<IKaleidoEventWriter>();
        outbox
            .Setup(x => x.Prepare(It.IsAny<KaleidoEventEnvelope<StepCompleted, ProcessEventContext>>()))
            .Returns(stepEvent);
        var savedEvents = new List<IReadOnlyCollection<KaleidoOutboxEvent>>();

        await CreateSut(SucceedingInvoker(), transition: RecordingTransition(savedEvents), eventOutbox: outbox)
            .ExecuteAsync([candidate], CreateContext("step-a"), TestContext.Current.CancellationToken);

        Assert.Equal(stepEvent, Assert.Single(Assert.Single(savedEvents)));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAPublisher_SavesTheStateWithoutEvents()
    {
        var registration = CreateRegistration<TestStepA>("step-a");
        var candidate = CreateCandidate<TestStepA>("step-a", registration);
        var outbox = new Mock<IKaleidoEventWriter>();
        var savedEvents = new List<IReadOnlyCollection<KaleidoOutboxEvent>>();

        await CreateSut(SucceedingInvoker(), transition: RecordingTransition(savedEvents), eventOutbox: outbox)
            .ExecuteAsync([candidate], CreateContext("step-a"), TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(savedEvents));
    }

    [Fact]
    public async Task ExecuteAsync_ARejectedStep_SavesNothingAndWritesNoEvent()
    {
        var registration = CreateRegistration<TestStepA>("step-a");
        var candidate = CreateCandidate<TestStepA>("step-a", registration);
        var evaluator = new Mock<IStepExecutionEvaluator>();
        evaluator
            .Setup(x => x.Evaluate(
                It.IsAny<StepCandidate>(),
                It.IsAny<StepInvocationResult>(),
                It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                It.IsAny<ProcessorContext>()))
            .Returns(ExecutionDecision.Rejected());
        var outbox = new Mock<IKaleidoEventWriter>();
        var savedEvents = new List<IReadOnlyCollection<KaleidoOutboxEvent>>();

        await CreateSut(SucceedingInvoker(), evaluator: evaluator, transition: RecordingTransition(savedEvents), eventOutbox: outbox)
            .ExecuteAsync([candidate], CreateContext("step-a"), TestContext.Current.CancellationToken);

        Assert.Empty(savedEvents);
        outbox.Verify(x => x.Prepare(It.IsAny<KaleidoEventEnvelope<StepCompleted, ProcessEventContext>>()), Times.Never);
    }

    // Like the real transition (its stores): a save with a cancelled token throws.
    private static Mock<IProcessorTransition> CancellationHonoringTransition(List<(ProcessorContext Context, IReadOnlyCollection<KaleidoOutboxEvent> Events)> saves)
    {
        var store = new Mock<IProcessorTransition>();
        store
            .Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()))
            .Returns((ProcessorContext saved, KaleidoOutboxEvent? transitionEvent, CancellationToken token) =>
            {
                token.ThrowIfCancellationRequested();
                saves.Add((saved, transitionEvent is null ? [] : [transitionEvent]));
                return Task.FromResult(saved with { Version = saved.Version + 1 });
            });

        return store;
    }

    [Fact]
    public async Task ExecuteAsync_CancelledWhileTheHandlerRuns_SavesTheStepAsCanceled()
    {
        var candidate = CreateCandidate<TestStepA>("step-a", CreateRegistration<TestStepA>("step-a"));
        using var cancellation = new CancellationTokenSource();
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessStepRegistration>(), It.IsAny<object>(), It.IsAny<ProcessStepContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (ProcessStepRegistration _, object _, ProcessStepContext _, CancellationToken token) =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                return CreateInvokerResult();
            });
        var saves = new List<(ProcessorContext Context, IReadOnlyCollection<KaleidoOutboxEvent> Events)>();
        var outbox = new Mock<IKaleidoEventWriter>();

        var result = await CreateSut(invoker, transition: CancellationHonoringTransition(saves), eventOutbox: outbox)
            .ExecuteAsync([candidate], CreateContext("step-a"), cancellation.Token);

        Assert.Equal(StepExecutionStatus.Canceled, Assert.Single(result.Outcomes).Status);
        var (saved, events) = Assert.Single(saves);
        Assert.Equal(CreateContext("step-a").State, saved.State);
        Assert.Empty(events);
        outbox.Verify(x => x.Prepare(It.IsAny<KaleidoEventEnvelope<StepCompleted, ProcessEventContext>>()), Times.Never);
    }

    // Only the caller's token is a cancellation (#517): a handler's downstream timeout (an
    // OperationCanceledException nobody asked for) is a failure - the step is an exception, observed
    // as a failure, and retryable.
    [Fact]
    public async Task ExecuteAsync_AHandlerTimeout_IsAFailure_NotACancellation()
    {
        var candidate = CreateCandidate<TestStepA>("step-a", CreateRegistration<TestStepA>("step-a"));
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessStepRegistration>(), It.IsAny<object>(), It.IsAny<ProcessStepContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The downstream call timed out.", new TimeoutException()));
        var stepObservation = new Mock<IProcessorStepObservation>();
        var stateUpdater = CreateStateUpdater();

        var result = await CreateSut(invoker, stateUpdater: stateUpdater, stepObservation: stepObservation)
            .ExecuteAsync([candidate], CreateContext("step-a"), TestContext.Current.CancellationToken);

        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(StepExecutionStatus.Exception, outcome.Status);
        Assert.Contains(outcome.RuntimeMessages, x => x.Code == ProcessorErrorCodes.FrameworkException);
        stepObservation.Verify(x => x.StepFailed(It.IsAny<TaskCanceledException>()), Times.Once);
        stepObservation.Verify(x => x.Canceled(), Times.Never);
        stateUpdater.Verify(x => x.ApplyUnfinishedStep(It.IsAny<ProcessorContext>(), candidate, StepExecutionStatus.Exception), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledAfterTheHandlerReturned_SavesTheCompletionAndItsEvent()
    {
        var candidate = CreateCandidate<TestStepA>("step-a", CreateRegistration<TestStepA>("step-a"));
        using var cancellation = new CancellationTokenSource();
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessStepRegistration>(), It.IsAny<object>(), It.IsAny<ProcessStepContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (ProcessStepRegistration _, object _, ProcessStepContext _, CancellationToken _) =>
            {
                await cancellation.CancelAsync();
                return CreateInvokerResult();
            });
        var stepEvent = OutboxEvent();
        var outbox = new Mock<IKaleidoEventWriter>();
        outbox
            .Setup(x => x.Prepare(It.IsAny<KaleidoEventEnvelope<StepCompleted, ProcessEventContext>>()))
            .Returns(stepEvent);
        var saves = new List<(ProcessorContext Context, IReadOnlyCollection<KaleidoOutboxEvent> Events)>();

        var result = await CreateSut(invoker, transition: CancellationHonoringTransition(saves), eventOutbox: outbox)
            .ExecuteAsync([candidate], CreateContext("step-a"), cancellation.Token);

        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(result.Outcomes).Status);
        var (saved, events) = Assert.Single(saves);
        Assert.Equal(stepEvent, Assert.Single(events));
    }

    [Fact]
    public async Task ExecuteAsync_CancelledAfterTheFirstStepReturned_CompletesItAndDoesNotInvokeTheNext()
    {
        var first = CreateCandidate<TestStepA>("step-a", CreateRegistration<TestStepA>("step-a"));
        var second = CreateCandidate<TestStepA>("step-b", CreateRegistration<TestStepA>("step-b"));
        using var cancellation = new CancellationTokenSource();
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessStepRegistration>(), It.IsAny<object>(), It.IsAny<ProcessStepContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (ProcessStepRegistration _, object _, ProcessStepContext _, CancellationToken _) =>
            {
                await cancellation.CancelAsync();
                return CreateInvokerResult();
            });
        var evaluator = new Mock<IStepExecutionEvaluator>();
        evaluator
            .Setup(x => x.Evaluate(first, It.IsAny<StepInvocationResult>(), It.IsAny<IReadOnlyCollection<StepCandidate>>(), It.IsAny<ProcessorContext>()))
            .Returns(ExecutionDecision.Continue(second));
        var saves = new List<(ProcessorContext Context, IReadOnlyCollection<KaleidoOutboxEvent> Events)>();

        var result = await CreateSut(invoker, evaluator: evaluator, transition: CancellationHonoringTransition(saves))
            .ExecuteAsync([first, second], CreateContext("step-a", "step-b"), cancellation.Token);

        Assert.Collection(
            result.Outcomes,
            x => Assert.Equal((first.StepName, StepExecutionStatus.Completed), (x.StepName, x.Status)),
            x => Assert.Equal((second.StepName, StepExecutionStatus.Canceled), (x.StepName, x.Status)));
        invoker.Verify(
            x => x.ExecuteAsync(It.IsAny<ProcessStepRegistration>(), It.IsAny<object>(), It.IsAny<ProcessStepContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(2, saves.Count);
    }

    [Fact]
    public async Task ExecuteAsync_AFailedStep_SavesItsStateWithoutAStepEvent()
    {
        var registration = CreateRegistration<TestStepA>("step-a");
        var candidate = CreateCandidate<TestStepA>("step-a", registration);
        var invoker = new Mock<IProcessStepInvoker>();
        invoker
            .Setup(x => x.ExecuteAsync(
                It.IsAny<ProcessStepRegistration>(),
                It.IsAny<object>(),
                It.IsAny<ProcessStepContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var savedEvents = new List<IReadOnlyCollection<KaleidoOutboxEvent>>();

        await CreateSut(invoker, transition: RecordingTransition(savedEvents))
            .ExecuteAsync([candidate], CreateContext("step-a"), TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(savedEvents));
    }
}
