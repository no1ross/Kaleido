using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;
using Kaleido.Process.Registry;

namespace Kaleido.Process.UnitTests.Processor.Execution;

public sealed class ExecutionProcessorTests
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
            .Returns(["step-b"]);

        var processor =
            CreateProcessor(
                invoker,
                evaluator,
                stateUpdater,
                stateRepository,
                availabilityResolver: availabilityResolver);

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
            context,
            new ProcessorRequest());

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
                AvailableSteps = ["step-b"]
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
                context,
                new ProcessorRequest());

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
                context,
                new ProcessorRequest());

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
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var availabilityResolver =
            new Mock<IStepAvailabilityResolver>();

        availabilityResolver
            .Setup(x =>
                x.Resolve(
                    It.IsAny<StepCandidate>(),
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>()))
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
                context1,
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

        stateRepository.Verify(
            x =>
                x.SaveAsync(
                    It.IsAny<ProcessorContext>(),
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
                context,
                new ProcessorRequest());

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
                new ProcessorRequest(),
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
            x => x.Code == StepProcessingMessageCode.ExecutionCanceled);

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
                context,
                new ProcessorRequest());

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
                context,
                new ProcessorRequest());

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
                context,
                new ProcessorRequest());

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
        ProcessorContext context,
        ProcessorContext updatedContext,
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
        Mock<IStepAvailabilityResolver>? availabilityResolver = null)
    {
        var correlationAccessor = new Mock<IKaleidoCorrelationContextAccessor>();
        correlationAccessor
            .SetupGet(x => x.Current)
            .Returns(new KaleidoCorrelationContext { RequestId = "test-request" });

        return new ExecutionProcessor(
            (invoker ?? new Mock<IProcessStepInvoker>()).Object,
            (evaluator ?? new Mock<IStepExecutionEvaluator>()).Object,
            (stateUpdater ?? new Mock<IProcessStateUpdater>()).Object,
            (stateRepository ?? new Mock<IProcessContextStore>()).Object,
            (availabilityResolver ?? new Mock<IStepAvailabilityResolver>()).Object,
            CreateProcessEventFactory().Object,
            CreateEventPublisher().Object,
            CreateObservability().Object,
            correlationAccessor.Object);
    }

    private static Mock<IProcessObservability> CreateObservability()
    {
        var stepObservation =
            new Mock<IProcessStepObservation>();

        stepObservation
            .Setup(x => x.Dispose());

        var observability =
            new Mock<IProcessObservability>();

        observability
            .Setup(x =>
                x.BeginStep(
                    It.IsAny<ProcessStepObservationDetails>()))
            .Returns(stepObservation.Object);

        return observability;
    }

    private static Mock<IEventPublisher> CreateEventPublisher()
    {
        var publisher =
            new Mock<IEventPublisher>(MockBehavior.Strict);

        publisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<KaleidoEventEnvelope<StepCompleted, ProcessEventContext>>(),
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
                x.CreateStepCompleted(
                    It.IsAny<KaleidoCorrelationContext>(),
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<StepCandidate>(),
                    It.IsAny<ProcessExecutionOutcome>(),
                    It.IsAny<ProcessStepInvokerResult>()))
            .Returns<KaleidoCorrelationContext, ProcessorContext, StepCandidate, ProcessExecutionOutcome, ProcessStepInvokerResult>((_, context, candidate, outcome, _2) =>
            {
                var stepContext = context.FindStep(candidate.StepName);
                return new KaleidoEventEnvelope<StepCompleted, ProcessEventContext>
                {
                    EventType = "process.step-completed.v1",
                    Context = CreateStubContext(context.ProcessId),
                    Event = new StepCompleted
                    {
                        OccurredOn = DateTimeOffset.UtcNow,
                        StepName = candidate.StepName,
                        StepVersion = candidate.Registration?.Metadata.Version ?? string.Empty,
                        Request = candidate.Step,
                        Response = outcome.Response,
                        DecisionType = outcome.Decision,
                        ExecutionStatus = outcome.Status,
                        Outcome = outcome.Outcome,
                        BusinessMessages = outcome.BusinessMessages,
                        RuntimeMessages = outcome.RuntimeMessages,
                        ProcessState = context.State,
                        RequiredStep = context.RequiredStep,
                        AvailableSteps = context.AvailableSteps,
                        StepLatestRequestId = stepContext?.LatestRequestId,
                        StepLastExecuted = stepContext?.LastExecuted
                    }
                };
            });

        return factory;
    }

    private static Mock<IStepAvailabilityResolver> CreateAvailabilityResolver(
        StepCandidate candidate,
        ProcessorContext context,
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