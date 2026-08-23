using Kaleido.Process.Observability;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Moq;

namespace Kaleido.Process.UnitTests.Participant.Execution;

public sealed class ExecutionProcessorTests
{
    [Fact]
    public void Constructor_WhenInvokerIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutionProcessor(
                null!,
                Mock.Of<IStepExecutionEvaluator>(),
                Mock.Of<IProcessStateUpdater>(),
                Mock.Of<IProcessContextStore>(),
                Mock.Of<IProcessStepRegistry>(),
                Mock.Of<IStepAvailabilityResolver>(),
                Mock.Of<IProcessObservability>()));
    }

    [Fact]
    public void Constructor_WhenEvaluatorIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutionProcessor(
                Mock.Of<IProcessStepInvoker>(),
                null!,
                Mock.Of<IProcessStateUpdater>(),
                Mock.Of<IProcessContextStore>(),
                Mock.Of<IProcessStepRegistry>(),
                Mock.Of<IStepAvailabilityResolver>(),
                Mock.Of<IProcessObservability>()));
    }

    [Fact]
    public void Constructor_WhenStateUpdaterIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutionProcessor(
                Mock.Of<IProcessStepInvoker>(),
                Mock.Of<IStepExecutionEvaluator>(),
                null!,
                Mock.Of<IProcessContextStore>(),
                Mock.Of<IProcessStepRegistry>(),
                Mock.Of<IStepAvailabilityResolver>(),
                Mock.Of<IProcessObservability>()));
    }

    [Fact]
    public void Constructor_WhenStateRepositoryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutionProcessor(
                Mock.Of<IProcessStepInvoker>(),
                Mock.Of<IStepExecutionEvaluator>(),
                Mock.Of<IProcessStateUpdater>(),
                null!,
                Mock.Of<IProcessStepRegistry>(),
                Mock.Of<IStepAvailabilityResolver>(),
                Mock.Of<IProcessObservability>()));
    }

    [Fact]
    public void Constructor_WhenStepRegistryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutionProcessor(
                Mock.Of<IProcessStepInvoker>(),
                Mock.Of<IStepExecutionEvaluator>(),
                Mock.Of<IProcessStateUpdater>(),
                Mock.Of<IProcessContextStore>(),
                null!,
                Mock.Of<IStepAvailabilityResolver>(),
                Mock.Of<IProcessObservability>()));
    }

    [Fact]
    public void Constructor_WhenAvailabilityResolverIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecutionProcessor(
                Mock.Of<IProcessStepInvoker>(),
                Mock.Of<IStepExecutionEvaluator>(),
                Mock.Of<IProcessStateUpdater>(),
                Mock.Of<IProcessContextStore>(),
                Mock.Of<IProcessStepRegistry>(),
                null!,
                Mock.Of<IProcessObservability>()));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidatesIsNull_Throws()
    {
        var processor =
            CreateProcessor();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            processor.ExecuteAsync(
                null!,
                CreateContext("step-a")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextIsNull_Throws()
    {
        var processor =
            CreateProcessor();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            processor.ExecuteAsync(
                [],
                null!));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCandidates_ReturnsCurrentContextState()
    {
        var context =
            CreateContext("step-a") with
            {
                AvailableSteps =
                [
                    "step-a",
                    "step-b"
                ],
                RequiredStep = "step-a"
            };

        var invoker =
            new Mock<IProcessStepInvoker>();

        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        var stateRepository =
            new Mock<IProcessContextStore>();

        var availabilityResolver =
            new Mock<IStepAvailabilityResolver>();

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository,
                availabilityResolver: availabilityResolver);

        var result =
            await processor.ExecuteAsync(
                [],
                context);

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
        evaluator.VerifyNoOtherCalls();
        stateUpdater.VerifyNoOtherCalls();
        stateRepository.VerifyNoOtherCalls();
        availabilityResolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_PassesAvailableNextStepsToProcessStepContext()
    {
        var candidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        ProcessStepContext? capturedContext =
            null;

        var invokerResult =
            CreateInvokerResult();

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
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context))
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

        var availabilityResolver =
            new Mock<IStepAvailabilityResolver>();

        availabilityResolver
            .Setup(x =>
                x.Resolve(
                    candidate,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context))
            .Returns(
            [
                "step-b"
            ]);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository,
                availabilityResolver: availabilityResolver);

        await processor.ExecuteAsync(
            [candidate],
            context);

        Assert.NotNull(
            capturedContext);

        Assert.Contains(
            "step-b",
            capturedContext.AvailableNextSteps);
    }

    [Fact]
    public async Task ExecuteAsync_InvokesCurrentCandidate()
    {
        var candidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        var invokerResult =
            CreateInvokerResult();

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
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context))
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

        var availabilityResolver =
            CreateAvailabilityResolver(
                candidate,
                context);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository,
                availabilityResolver: availabilityResolver);

        await processor.ExecuteAsync(
            [candidate],
            context);

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
        var candidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        var invokerResult =
            CreateInvokerResult();

        var decision =
            ExecutionDecision.Complete();

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
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
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context))
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
                availabilityResolver:
                    CreateAvailabilityResolver(
                        candidate,
                        context));

        await processor.ExecuteAsync(
            [candidate],
            context);

        evaluator.Verify(
            x =>
                x.Evaluate(
                    candidate,
                    invokerResult,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AppliesExecutionAndPersistsUpdatedContext()
    {
        var candidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a") with
            {
                AvailableSteps =
                [
                    "step-b"
                ]
            };

        var invokerResult =
            CreateInvokerResult();

        var decision =
            ExecutionDecision.Complete();

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
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
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context))
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
                availabilityResolver:
                    CreateAvailabilityResolver(
                        candidate,
                        context));

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        stateUpdater.Verify(
            x =>
                x.ApplyExecution(
                    context,
                    candidate,
                    decision),
            Times.Once);

        stateRepository.Verify(
            x =>
                x.SaveAsync(
                    updatedContext,
                    It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(
            updatedContext.State,
            result.State);

        Assert.Equal(
            updatedContext.RequiredStep,
            result.RequiredStep);

        Assert.Equal(
            updatedContext.AvailableSteps,
            result.AvailableSteps);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDecisionIsComplete_ReturnsSingleCompletedOutcome()
    {
        var candidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var response =
            new TestStepResponse();

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        var invokerResult =
            CreateInvokerResult(
                response);

        var decision =
            ExecutionDecision.Complete();

        var processor =
            CreateProcessorForSuccessfulExecution(
                candidate,
                context,
                updatedContext,
                invokerResult,
                decision);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        var outcome =
            Assert.Single(
                result.Outcomes);

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
        var firstCandidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var nextCandidate =
            CreateCandidate<TestStepB>(
                "step-b");

        var context1 =
            CreateContext(
                "step-a",
                "step-b");

        var context2 =
            CreateContext(
                "step-a",
                "step-b");

        var context3 =
            CreateContext(
                "step-a",
                "step-b");

        var firstResult =
            CreateInvokerResult();

        var secondResult =
            CreateInvokerResult();

        var continueDecision =
            ExecutionDecision.Continue(
                nextCandidate);

        var completeDecision =
            ExecutionDecision.Complete();

        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .SetupSequence(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstResult)
            .ReturnsAsync(secondResult);

        var evaluator =
            new Mock<IStepExecutionEvaluator>();

        evaluator
            .Setup(x =>
                x.Evaluate(
                    firstCandidate,
                    firstResult,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context1))
            .Returns(continueDecision);

        evaluator
            .Setup(x =>
                x.Evaluate(
                    nextCandidate,
                    secondResult,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context2))
            .Returns(completeDecision);

        var stateUpdater =
            new Mock<IProcessStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    context1,
                    firstCandidate,
                    continueDecision))
            .Returns(context2);

        stateUpdater
            .Setup(x =>
                x.ApplyExecution(
                    context2,
                    nextCandidate,
                    completeDecision))
            .Returns(context3);

        var stateRepository =
            new Mock<IProcessContextStore>();

        stateRepository
            .Setup(x =>
                x.SaveAsync(
                    It.IsAny<ParticipantContext>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var availabilityResolver =
            new Mock<IStepAvailabilityResolver>();

        availabilityResolver
            .Setup(x =>
                x.Resolve(
                    It.IsAny<StepCandidate>(),
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ParticipantContext>()))
            .Returns([]);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository,
                availabilityResolver: availabilityResolver);

        var result =
            await processor.ExecuteAsync(
                [
                    firstCandidate,
                    nextCandidate
                ],
                context1);

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

        stateRepository.Verify(
            x =>
                x.SaveAsync(
                    It.IsAny<ParticipantContext>(),
                    It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvokerThrows_AppliesExceptionPersistsAndReturnsExceptionOutcome()
    {
        var candidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
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
            .ThrowsAsync(
                new InvalidOperationException(
                    "boom"));

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
                invoker,
                stateUpdater: stateUpdater,
                stateRepository: stateRepository,
                availabilityResolver:
                    CreateAvailabilityResolver(
                        candidate,
                        context));

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context);

        var outcome =
            Assert.Single(
                result.Outcomes);

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

        stateUpdater.Verify(
            x =>
                x.ApplyException(
                    context,
                    candidate),
            Times.Once);

        stateRepository.Verify(
            x =>
                x.SaveAsync(
                    updatedContext,
                    CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_AppliesCancellationPersistsAndReturnsCanceledOutcome()
    {
        var candidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var context =
            CreateContext("step-a");

        var updatedContext =
            CreateContext("step-a");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

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
            new Mock<IProcessStepInvoker>();

        var processor =
            CreateProcessor(
                invoker,
                stateUpdater: stateUpdater,
                stateRepository: stateRepository);

        var result =
            await processor.ExecuteAsync(
                [candidate],
                context,
                cancellationTokenSource.Token);

        var outcome =
            Assert.Single(
                result.Outcomes);

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
            x => x.Code == StepProcessingMessageCode.ExecutionCancelled);

        invoker.Verify(
            x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
                    It.IsAny<ProcessStepContext>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        stateUpdater.Verify(
            x =>
                x.ApplyCancellation(
                    context,
                    candidate),
            Times.Once);

        stateRepository.Verify(
            x =>
                x.SaveAsync(
                    updatedContext,
                    CancellationToken.None),
            Times.Once);
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
            Assert.Single(
                result.Outcomes);

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

    [Fact]
    public async Task ExecuteAsync_WhenCandidateStepIsMissing_ReturnsExceptionOutcome()
    {
        var candidate =
            CreateCandidate(
                "step-a",
                CreateRegistration<TestStepA>(
                    "step-a"),
                step: null);

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
            Assert.Single(
                result.Outcomes);

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

    [Fact]
    public async Task ExecuteAsync_WhenStepContextIsMissing_ReturnsExceptionOutcome()
    {
        var candidate =
            CreateCandidate<TestStepA>(
                "step-a");

        var context =
            CreateContext("different-step");

        var updatedContext =
            CreateContext("different-step");

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
            Assert.Single(
                result.Outcomes);

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

    private static ExecutionProcessor CreateProcessorForSuccessfulExecution(
        StepCandidate candidate,
        ParticipantContext context,
        ParticipantContext updatedContext,
        ProcessStepInvokerResult invokerResult,
        ExecutionDecision decision)
    {
        var invoker =
            new Mock<IProcessStepInvoker>();

        invoker
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<ProcessStepRegistration>(),
                    It.IsAny<object>(),
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
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context))
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

        return CreateProcessor(
            invoker,
            evaluator,
            stateUpdater,
            stateRepository,
            availabilityResolver:
                CreateAvailabilityResolver(
                    candidate,
                    context));
    }

    private static ExecutionProcessor CreateProcessor(
        Mock<IProcessStepInvoker>? invoker = null,
        Mock<IStepExecutionEvaluator>? evaluator = null,
        Mock<IProcessStateUpdater>? stateUpdater = null,
        Mock<IProcessContextStore>? stateRepository = null,
        Mock<IProcessStepRegistry>? stepRegistry = null,
        Mock<IStepAvailabilityResolver>? availabilityResolver = null)
    {
        return new ExecutionProcessor(
            (invoker ?? new Mock<IProcessStepInvoker>()).Object,
            (evaluator ?? new Mock<IStepExecutionEvaluator>()).Object,
            (stateUpdater ?? new Mock<IProcessStateUpdater>()).Object,
            (stateRepository ?? new Mock<IProcessContextStore>()).Object,
            (stepRegistry ?? new Mock<IProcessStepRegistry>()).Object,
            (availabilityResolver ?? new Mock<IStepAvailabilityResolver>()).Object,
            Mock.Of<IProcessObservability>());
    }

    private static Mock<IStepAvailabilityResolver> CreateAvailabilityResolver(
        StepCandidate candidate,
        ParticipantContext context,
        IReadOnlyCollection<string>? availableSteps = null)
    {
        var availabilityResolver =
            new Mock<IStepAvailabilityResolver>();

        availabilityResolver
            .Setup(x =>
                x.Resolve(
                    candidate,
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context))
            .Returns(
                availableSteps ?? []);

        return availabilityResolver;
    }

    private static ProcessStepInvokerResult CreateInvokerResult(
        object? response = null)
    {
        return new ProcessStepInvokerResult
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
            CreateRegistration<TStep>(
                stepName),
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
                step
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

    private static ParticipantContext CreateContext(
        params string[] stepNames)
    {
        return new ParticipantContext
        {
            ProcessId =
                Guid.NewGuid(),

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
}