using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Moq;
using Xunit;

namespace Kaleido.Process.Tests.Participant.Execution;

public sealed class ExecutionProcessorTests
{
    [Fact]
    public void Constructor_WhenInvokerIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionProcessor(
                    null!,
                    Mock.Of<IStepExecutionEvaluator>(),
                    Mock.Of<IProcessStateUpdater>(),
                    Mock.Of<IProcessContextStore>(),
                    Mock.Of<IProcessStepRegistry>()));

        Assert.Equal("invoker", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenEvaluatorIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionProcessor(
                    Mock.Of<IProcessStepInvoker>(),
                    null!,
                    Mock.Of<IProcessStateUpdater>(),
                    Mock.Of<IProcessContextStore>(),
                    Mock.Of<IProcessStepRegistry>()));

        Assert.Equal("evaluator", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenStateUpdaterIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionProcessor(
                    Mock.Of<IProcessStepInvoker>(),
                    Mock.Of<IStepExecutionEvaluator>(),
                    null!,
                    Mock.Of<IProcessContextStore>(),
                    Mock.Of<IProcessStepRegistry>()));

        Assert.Equal("stateUpdater", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenStateRepositoryIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionProcessor(
                    Mock.Of<IProcessStepInvoker>(),
                    Mock.Of<IStepExecutionEvaluator>(),
                    Mock.Of<IProcessStateUpdater>(),
                    null!,
                    Mock.Of<IProcessStepRegistry>()));

        Assert.Equal("stateRepository", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenStepRegistryIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ExecutionProcessor(
                    Mock.Of<IProcessStepInvoker>(),
                    Mock.Of<IStepExecutionEvaluator>(),
                    Mock.Of<IProcessStateUpdater>(),
                    Mock.Of<IProcessContextStore>(),
                    null!));

        Assert.Equal("stepRegistry", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidatesIsNull_Throws()
    {
        var processor =
            CreateProcessor();

        var exception =
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                processor.ExecuteAsync(
                    null!,
                    CreateContext("step-a")));

        Assert.Equal("candidates", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextIsNull_Throws()
    {
        var processor =
            CreateProcessor();

        var exception =
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                processor.ExecuteAsync(
                    [],
                    null!));

        Assert.Equal("context", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCandidates_ReturnsCurrentStateWithoutOutcomes()
    {
        var context =
            CreateContext("step-a");

        var processor =
            CreateProcessor();

        var result =
            await processor.ExecuteAsync(
                [],
                context);

        Assert.Equal(context.State, result.State);
        Assert.Equal(context.RequiredStep, result.RequiredStep);
        Assert.Equal(context.AvailableSteps, result.AvailableSteps);
        Assert.Empty(result.Outcomes);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSingleStepCompletes_ReturnsCompletedOutcome()
    {
        var candidate =
            CreateCandidate<TestStepA>("step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        var invokerResult =
            new ProcessStepInvokerResult
            {
                Succeeded = true,
                Response =
                    new TestStepResponse
                    {
                        Value = "response-a"
                    }
            };

        var decision =
            ExecutionDecision.Complete();

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    candidate.Registration!,
                    candidate.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(invokerResult);

        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        evaluator
            .Setup(x =>
                x.Evaluate(
                    candidate,
                    invokerResult,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>()))
            .Returns(decision);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    context,
                    candidate,
                    decision))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal("step-a", outcome.StepName);
        Assert.Equal(StepExecutionStatus.Completed, outcome.Status);
        Assert.Equal(ExecutionDecisionType.Complete, outcome.Decision);

        var response =
            Assert.IsType<TestStepResponse>(outcome.Response);

        Assert.Equal("response-a", response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExecutionSucceeds_AppliesExecutionAndSavesState()
    {
        var candidate =
            CreateCandidate<TestStepA>("step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        var invokerResult =
            new ProcessStepInvokerResult
            {
                Succeeded = true,
                Response = new TestStepResponse()
            };

        var decision =
            ExecutionDecision.Complete();

        var sequence =
            new MockSequence();

        var invoker =
            new Mock<IProcessStepInvoker>(MockBehavior.Strict);

        invoker
            .InSequence(sequence)
            .Setup(x =>
                x.ExecuteAsync(
                    candidate.Registration!,
                    candidate.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(invokerResult);

        var evaluator =
            new Mock<IStepExecutionEvaluator>(MockBehavior.Strict);

        evaluator
            .InSequence(sequence)
            .Setup(x =>
                x.Evaluate(
                    candidate,
                    invokerResult,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>()))
            .Returns(decision);

        var stateUpdater =
            new Mock<IProcessStateUpdater>(MockBehavior.Strict);

        stateUpdater
            .InSequence(sequence)
            .Setup(x =>
                x.ApplyExecution(
                    context,
                    candidate,
                    decision))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>(MockBehavior.Strict);

        stateRepository
            .InSequence(sequence)
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository);

        await processor.ExecuteAsync(
            [candidate],
            context);

        invoker.VerifyAll();
        evaluator.VerifyAll();
        stateUpdater.VerifyAll();
        stateRepository.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDecisionIsContinue_ExecutesNextCandidate()
    {
        var candidateA =
            CreateCandidate<TestStepA>("step-a");

        var candidateB =
            CreateCandidate<TestStepB>("step-b");

        var context =
            CreateContext(
                "step-a",
                "step-b");

        var contextAfterA =
            CreateContext(
                "step-a",
                "step-b");

        var contextAfterB =
            CreateContext(
                "step-a",
                "step-b");

        var resultA =
            new ProcessStepInvokerResult
            {
                Succeeded = true,
                Response = new TestStepResponse()
            };

        var resultB =
            new ProcessStepInvokerResult
            {
                Succeeded = true,
                Response = new TestStepResponse()
            };

        var continueDecision =
            ExecutionDecision.Continue(candidateB);

        var completeDecision =
            ExecutionDecision.Complete();

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    candidateA.Registration!,
                    candidateA.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultA);

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    candidateB.Registration!,
                    candidateB.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultB);

        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        evaluator
            .Setup(x =>
                x.Evaluate(
                    candidateA,
                    resultA,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>()))
            .Returns(continueDecision);

        evaluator
            .Setup(x =>
                x.Evaluate(
                    candidateB,
                    resultB,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>()))
            .Returns(completeDecision);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    context,
                    candidateA,
                    continueDecision))
            .Returns(contextAfterA);

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    contextAfterA,
                    candidateB,
                    completeDecision))
            .Returns(contextAfterB);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    It.IsAny<ParticipantContext>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository);

        var result =
            await processor.ExecuteAsync(
                [
                    candidateA,
                    candidateB
                ],
                context);

        Assert.Equal(2, result.Outcomes.Count);
        Assert.Equal("step-a", result.Outcomes[0].StepName);
        Assert.Equal("step-b", result.Outcomes[1].StepName);

        invoker.Verify(
            x =>
                x.ExecuteAsync(
                    candidateA.Registration!,
                    candidateA.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()),
            Times.Once);

        invoker.Verify(
            x =>
                x.ExecuteAsync(
                    candidateB.Registration!,
                    candidateB.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDecisionIsBusinessFailure_StopsProcessing()
    {
        var candidateA =
            CreateCandidate<TestStepA>("step-a");

        var candidateB =
            CreateCandidate<TestStepB>("step-b");

        var context =
            CreateContext(
                "step-a",
                "step-b");

        var updatedContext =
            CreateContext(
                "step-a",
                "step-b");

        var invokerResult =
            new ProcessStepInvokerResult
            {
                Succeeded = false,
                Response = new TestStepResponse()
            };

        var decision =
            ExecutionDecision.BusinessFailure();

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    candidateA.Registration!,
                    candidateA.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(invokerResult);

        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        evaluator
            .Setup(x =>
                x.Evaluate(
                    candidateA,
                    invokerResult,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>()))
            .Returns(decision);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    context,
                    candidateA,
                    decision))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository);

        var result =
            await processor.ExecuteAsync(
                [
                    candidateA,
                    candidateB
                ],
                context);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal("step-a", outcome.StepName);
        Assert.Equal(ExecutionDecisionType.BusinessFailure, outcome.Decision);

        invoker.Verify(
            x =>
                x.ExecuteAsync(
                    candidateB.Registration!,
                    candidateB.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDecisionContainsMessages_AddsDecisionMessagesToOutcome()
    {
        var candidate =
            CreateCandidate<TestStepA>("step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        var invokerResult =
            new ProcessStepInvokerResult
            {
                Succeeded = true,
                Response = new TestStepResponse()
            };

        var decision =
            ExecutionDecision.ProcessViolation(
                StepProcessingMessage.Error(
                    StepProcessingMessageCode.RequiredStepNotAllowed,
                    "Required step was not allowed."));

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    candidate.Registration!,
                    candidate.Step!,
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(invokerResult);

        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        evaluator
            .Setup(x =>
                x.Evaluate(
                    candidate,
                    invokerResult,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>()))
            .Returns(decision);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    context,
                    candidate,
                    decision))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Contains(
            outcome.Messages,
            x => x.Code ==
                 StepProcessingMessageCode.RequiredStepNotAllowed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepIsMissingFromContext_AppliesExceptionAndSavesState()
    {
        var candidate =
            CreateCandidate<TestStepA>("step-a");

        var context =
            CreateContext("other-step");

        var updatedContext =
            CreateContext("other-step");

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyException(
                    context,
                    candidate))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    CancellationToken.None))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                stateUpdater: stateUpdater,
                stateRepository: stateRepository);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal("step-a", outcome.StepName);
        Assert.Equal(StepExecutionStatus.Exception, outcome.Status);
        Assert.Equal(ExecutionDecisionType.ProcessViolation, outcome.Decision);

        Assert.Contains(
            outcome.Messages,
            x => x.Code ==
                 StepProcessingMessageCode.FrameworkException);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidateHasNoRegistration_AppliesExceptionAndSavesState()
    {
        var candidate =
            new StepCandidate
            {
                StepName = "step-a",
                Step = new TestStepA(),
                Status = StepCandidateStatus.Built
            };

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyException(
                    context,
                    candidate))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    CancellationToken.None))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                stateUpdater: stateUpdater,
                stateRepository: stateRepository);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(StepExecutionStatus.Exception, outcome.Status);

        Assert.Contains(
            outcome.Messages,
            x => x.Code ==
                 StepProcessingMessageCode.FrameworkException);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidateHasNoStepInstance_AppliesExceptionAndSavesState()
    {
        var candidate =
            new StepCandidate
            {
                StepName = "step-a",
                Registration = CreateRegistration<TestStepA>("step-a"),
                Status = StepCandidateStatus.Built
            };

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyException(
                    context,
                    candidate))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    CancellationToken.None))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                stateUpdater: stateUpdater,
                stateRepository: stateRepository);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal(StepExecutionStatus.Exception, outcome.Status);

        Assert.Contains(
            outcome.Messages,
            x => x.Code ==
                 StepProcessingMessageCode.FrameworkException);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvokerThrows_AppliesExceptionAndSavesState()
    {
        var candidate =
            CreateCandidate<TestStepA>("step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
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
            .ThrowsAsync(
                new InvalidOperationException("boom"));

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyException(
                    context,
                    candidate))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    CancellationToken.None))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                invoker: invoker,
                stateUpdater: stateUpdater,
                stateRepository: stateRepository);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal("step-a", outcome.StepName);
        Assert.Equal(StepExecutionStatus.Exception, outcome.Status);
        Assert.Equal(ExecutionDecisionType.ProcessViolation, outcome.Decision);

        Assert.Contains(
            outcome.Messages,
            x =>
                x.Code == StepProcessingMessageCode.FrameworkException &&
                x.Message.Contains("boom"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_AppliesCancellationAndSavesState()
    {
        var candidate =
            CreateCandidate<TestStepA>("step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyCancellation(
                    context,
                    candidate))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    CancellationToken.None))
            .Returns(Task.CompletedTask);

        var invoker =
            new Mock<IProcessStepInvoker>(MockBehavior.Strict);

        var processor =
            CreateProcessor(
                invoker: invoker,
                stateUpdater: stateUpdater,
                stateRepository: stateRepository);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                cancellationTokenSource.Token);

        var outcome =
            Assert.Single(result.Outcomes);

        Assert.Equal("step-a", outcome.StepName);
        Assert.Equal(StepExecutionStatus.Canceled, outcome.Status);
        Assert.Equal(ExecutionDecisionType.ProcessViolation, outcome.Decision);

        Assert.Contains(
            outcome.Messages,
            x => x.Code ==
                 StepProcessingMessageCode.ExecutionCancelled);

        invoker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_PassesAvailableNextStepsToProcessStepContext()
    {
        var candidate =
            CreateCandidate<TestStepA>("step-a");

        var dependentRegistration =
            CreateRegistration<TestStepB>("step-b");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        ProcessStepContext? capturedContext =
            null;

        var invokerResult =
            new ProcessStepInvokerResult
            {
                Succeeded = true,
                Response = new TestStepResponse()
            };

        var decision =
            ExecutionDecision.Complete();

        var stepRegistry =
            new Mock<IProcessStepRegistry>();

        stepRegistry
            .Setup(x =>
                x.GetDependents(It.IsAny<Type>()))
            .Returns([dependentRegistration]);

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
            .ReturnsAsync(invokerResult);

        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        evaluator
            .Setup(x =>
                x.Evaluate(
                    candidate,
                    invokerResult,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>()))
            .Returns(decision);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    context,
                    candidate,
                    decision))
            .Returns(updatedContext);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    updatedContext,
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository,
                stepRegistry);

        await processor.ExecuteAsync(
            [candidate],
            context);

        Assert.NotNull(capturedContext);
        Assert.Contains("step-b", capturedContext.AvailableNextSteps);
    }

    private static ExecutionProcessor CreateProcessor(
        Mock<IProcessStepInvoker>? invoker = null,
        Mock<IStepExecutionEvaluator>? evaluator = null,
        Mock<IProcessStateUpdater>? stateUpdater = null,
        Mock<IProcessContextStore>? stateRepository = null,
        Mock<IProcessStepRegistry>? stepRegistry = null)
    {
        invoker ??= new Mock<IProcessStepInvoker>();
        evaluator ??= new Mock<IStepExecutionEvaluator>();
        stateUpdater ??= new Mock<IProcessStateUpdater>();
        stateRepository ??= new Mock<IProcessContextStore>();

        var createdStepRegistry =
            stepRegistry is null;

        stepRegistry ??= new Mock<IProcessStepRegistry>();

        if (createdStepRegistry)
        {
            stepRegistry
                .Setup(x =>
                    x.GetDependents(It.IsAny<Type>()))
                .Returns([]);
        }

        return new ExecutionProcessor(
            invoker.Object,
            evaluator.Object,
            stateUpdater.Object,
            stateRepository.Object,
            stepRegistry.Object);
    }

    private static StepCandidate CreateCandidate<TStep>(
        string stepName)
        where TStep : new()
    {
        return new StepCandidate
        {
            StepName = stepName,
            Registration =
                CreateRegistration<TStep>(
                    stepName),
            Step = new TStep(),
            Status = StepCandidateStatus.Built,
            IncludedInExecutionPlan = true
        };
    }

    private static ProcessStepRegistration CreateRegistration<TStep>(
        string name)
    {
        return new ProcessStepRegistration(
            typeof(TStep),
            typeof(TestStepResponse),
            typeof(object),
            new ProcessStepMetadata(
                name,
                $"{name} description.",
                "1.0"));
    }

    private static ParticipantContext CreateContext(
        params string[] stepNames)
    {
        return new ParticipantContext
        {
            Steps =
                stepNames
                    .Select(stepName =>
                        new StepContext
                        {
                            StepName = stepName,
                            Status = StepExecutionStatus.Pending
                        })
                    .ToArray()
        };
    }

    private sealed class TestStepA;

    private sealed class TestStepB;

    private sealed class TestStepResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}