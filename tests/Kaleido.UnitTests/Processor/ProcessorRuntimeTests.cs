using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;
using Kaleido.Processor.Registry;
using Kaleido.Storage;
using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests;

public sealed class ProcessorRuntimeTests
    : SutFixture
{
    private static ProcessorRuntime CreateSut(
        IKaleidoContextStore contextStore,
        IProcessorStateUpdater stateUpdater,
        IProcessorPlanner planner,
        IProcessorExecutor processor,
        IProcessorEventFactory eventFactory,
        IKaleidoEventWriter eventOutbox,
        IProcessorObservability observability,
        IKaleidoCorrelationContextAccessor correlationAccessor,
        List<KaleidoOutboxEvent?>? transitionEvents = null) =>
        new(contextStore, Transition(contextStore, transitionEvents), stateUpdater, planner, processor,
            eventFactory, eventOutbox, observability,
            correlationAccessor, new KaleidoServiceOptions { ServiceName = "radiology" });

    // The transition (its own fixture: ProcessorTransitionTests) saves through the store here and
    // records the event the runtime decided belongs to the save.
    private static IProcessorTransition Transition(IKaleidoContextStore contextStore, List<KaleidoOutboxEvent?>? transitionEvents)
    {
        var transition = new Mock<IProcessorTransition>();
        transition
            .Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()))
            .Returns((ProcessorContext context, KaleidoOutboxEvent? transitionEvent, CancellationToken cancellationToken) =>
            {
                transitionEvents?.Add(transitionEvent);
                return contextStore.SaveAsync(context, cancellationToken);
            });

        return transition.Object;
    }

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
            new Mock<IKaleidoContextStore>(MockBehavior.Strict);
        SetupSave(contextStore);

        var stateUpdater =
            new Mock<IProcessorStateUpdater>(MockBehavior.Strict);

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
            new Mock<IProcessorPlanner>(MockBehavior.Strict);

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
            new Mock<IProcessorExecutor>(MockBehavior.Strict);

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyCollection<StepCandidate>, ProcessorContext, CancellationToken>((_, context, _) =>
                Task.FromResult(
                    CreateExecutionResult(
                        context.ProcessId)));

        var runtime =
            CreateSut(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
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
    public async Task ExecuteAsync_WhenContextDoesNotExist_OnAHandOff_InitializesContext()
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
            new Mock<IKaleidoContextStore>();
        SetupSave(contextStore);

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessorContext?)null);

        var stateUpdater =
            new Mock<IProcessorStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Initialize(
                    request.ProcessId.Value))
            .Returns(CreateContext(
                request.ProcessId.Value,
                "ignored"));

        var planner =
            new Mock<IProcessorPlanner>();

        planner
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    It.IsAny<ProcessorContext>()))
            .Returns(new ExecutionPlanResult { Candidates = [] });

        var processor =
            new Mock<IProcessorExecutor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    It.IsAny<ProcessorContext>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            CreateSut(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor(callingProcessor: "intake"));

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

    [Theory]
    [InlineData(null)]
    [InlineData("radiology")]
    public async Task ExecuteAsync_WhenContextDoesNotExist_AndItIsNotAHandOff_StartsNothing(string? callingProcessor)
    {
        var request = CreateRequest();
        var processId = Assert.IsType<Guid>(request.ProcessId);

        var contextStore = new Mock<IKaleidoContextStore>();
        contextStore.Setup(x => x.LoadAsync(processId, It.IsAny<CancellationToken>())).ReturnsAsync(default(ProcessorContext));

        var stateUpdater = new Mock<IProcessorStateUpdater>(MockBehavior.Strict);
        var processor = new Mock<IProcessorExecutor>(MockBehavior.Strict);

        var runtime =
            CreateSut(
                contextStore.Object,
                stateUpdater.Object,
                new Mock<IProcessorPlanner>(MockBehavior.Strict).Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor(callingProcessor: callingProcessor));

        var exception = await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoValidationException>(() => runtime.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(ProcessorErrorCodes.ProcessNotFound, exception.Code);
        contextStore.Verify(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ACallerError_IsRecordedAsRejected_NotAsAFailure()
    {
        var request = CreateRequest();
        var processId = Assert.IsType<Guid>(request.ProcessId);

        var contextStore = new Mock<IKaleidoContextStore>();
        contextStore.Setup(x => x.LoadAsync(processId, It.IsAny<CancellationToken>())).ReturnsAsync(default(ProcessorContext));

        var execution = new Mock<IProcessorExecutionObservation>();
        var observability = new Mock<IProcessorObservability>();
        observability.Setup(x => x.BeginExecution(It.IsAny<ProcessorExecutionObservationDetails>())).Returns(execution.Object);

        var runtime =
            CreateSut(
                contextStore.Object,
                new Mock<IProcessorStateUpdater>(MockBehavior.Strict).Object,
                new Mock<IProcessorPlanner>(MockBehavior.Strict).Object,
                new Mock<IProcessorExecutor>(MockBehavior.Strict).Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
                observability.Object,
                CreateCorrelationAccessor());

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoValidationException>(() => runtime.ExecuteAsync(request, TestContext.Current.CancellationToken));

        execution.Verify(x => x.ExecutionRejected(It.IsAny<Kaleido.Exceptions.KaleidoValidationException>(), ProcessorErrorCodes.ProcessNotFound), Times.Once);
        execution.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Never);
    }

    public static TheoryData<Exception, string?> LoadFailures() =>
        new()
        {
            { new Kaleido.Exceptions.KaleidoConcurrencyException(Guid.Empty), new Kaleido.Exceptions.KaleidoConcurrencyException(Guid.Empty).Code },
            { new Kaleido.Exceptions.KaleidoAuthorizationException("Approve", callerIsAuthenticated: true), ProcessorTelemetry.AuthorizationRejectionCode },
            { new InvalidOperationException("store down"), null },
        };

    [Theory]
    [MemberData(nameof(LoadFailures))]
    public async Task ExecuteAsync_WhenLoadingFails_RecordsCallerErrorsAsRejectedAndTheRestAsFailures(Exception failure, string? rejectionCode)
    {
        var request = CreateRequest();
        var contextStore = new Mock<IKaleidoContextStore>();
        contextStore.Setup(x => x.LoadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var execution = new Mock<IProcessorExecutionObservation>();
        var observability = new Mock<IProcessorObservability>();
        observability.Setup(x => x.BeginExecution(It.IsAny<ProcessorExecutionObservationDetails>())).Returns(execution.Object);

        var runtime =
            CreateSut(
                contextStore.Object,
                new Mock<IProcessorStateUpdater>(MockBehavior.Strict).Object,
                new Mock<IProcessorPlanner>(MockBehavior.Strict).Object,
                new Mock<IProcessorExecutor>(MockBehavior.Strict).Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
                observability.Object,
                CreateCorrelationAccessor());

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => runtime.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);

        if (rejectionCode is null)
        {
            execution.Verify(x => x.ExecutionFailed(failure), Times.Once);
            execution.Verify(x => x.ExecutionRejected(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        }
        else
        {
            execution.Verify(x => x.ExecutionRejected(failure, rejectionCode), Times.Once);
            execution.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Never);
        }
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
            new Mock<IKaleidoContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContext);

        var stateUpdater =
            new Mock<IProcessorStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Reconcile(existingContext))
            .Returns(reconciledContext);

        var planner =
            new Mock<IProcessorPlanner>();

        planner
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    reconciledContext))
            .Returns(new ExecutionPlanResult { Candidates = [] });

        var processor =
            new Mock<IProcessorExecutor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    reconciledContext,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            CreateSut(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
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
            new Mock<IKaleidoContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var stateUpdater =
            new Mock<IProcessorStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Reconcile(context))
            .Returns(context);

        var planner =
            new Mock<IProcessorPlanner>();

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
            new Mock<IProcessorExecutor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context,
                    It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StepCandidate>, ProcessorContext, CancellationToken>(
                (candidates, _, _) => capturedCandidates = candidates)
            .ReturnsAsync(
                CreateExecutionResult(processId));

        var runtime =
            CreateSut(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
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
            ProcessorErrorCodes.InvalidRequest,
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
                        ProcessorErrorCodes.ProcessMessage,
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
            new Mock<IKaleidoContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var stateUpdater =
            new Mock<IProcessorStateUpdater>();

        stateUpdater
            .Setup(x =>
                x.Reconcile(context))
            .Returns(context);

        var planner =
            new Mock<IProcessorPlanner>();

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
            new Mock<IProcessorExecutor>();

        processor
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);

        var runtime =
            CreateSut(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
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
            new Mock<IKaleidoContextStore>(MockBehavior.Strict);

        contextStore
            .InSequence(sequence)
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var stateUpdater =
            new Mock<IProcessorStateUpdater>(MockBehavior.Strict);

        stateUpdater
            .InSequence(sequence)
            .Setup(x =>
                x.Reconcile(context))
            .Returns(context);

        var planner =
            new Mock<IProcessorPlanner>(MockBehavior.Strict);

        planner
            .InSequence(sequence)
            .Setup(x =>
                x.BuildPlan(
                    request.Processor,
                    context))
            .Returns(plan);

        var processor =
            new Mock<IProcessorExecutor>(MockBehavior.Strict);

        processor
            .InSequence(sequence)
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<IReadOnlyCollection<StepCandidate>>(),
                    context,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);

        var runtime =
            CreateSut(
                contextStore.Object,
                stateUpdater.Object,
                planner.Object,
                processor.Object,
                CreateProcessEventFactory().Object,
                new Mock<IKaleidoEventWriter>().Object,
                CreateObservability().Object,
                CreateCorrelationAccessor());

        await runtime.ExecuteAsync(request);

        contextStore.VerifyAll();
        stateUpdater.VerifyAll();
        planner.VerifyAll();
        processor.VerifyAll();
    }

    private static Mock<IProcessorObservability> CreateObservability()
    {
        var executionObservation =
            new Mock<IProcessorExecutionObservation>();

        executionObservation
            .Setup(x => x.Dispose());

        var observability =
            new Mock<IProcessorObservability>();

        observability
            .Setup(x =>
                x.BeginExecution(
                    It.IsAny<ProcessorExecutionObservationDetails>()))
            .Returns(executionObservation.Object);

        return observability;
    }

    private static void SetupSave(Mock<IKaleidoContextStore> store) =>
        store
            .Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessorContext saved, CancellationToken _) => saved with { Version = saved.Version + 1 });

    private static ProcessorRuntime CreateNewProcessSut(Mock<IKaleidoContextStore> store, Mock<IKaleidoEventWriter> outbox, Mock<IProcessorExecutor> processor, KaleidoCaller? caller = null, List<KaleidoOutboxEvent?>? transitionEvents = null)
    {
        var stateUpdater = new Mock<IProcessorStateUpdater>();
        stateUpdater.Setup(x => x.Initialize(It.IsAny<Guid>())).Returns<Guid>(id => CreateContext(id, "REQ-001"));

        var planner = new Mock<IProcessorPlanner>();
        planner.Setup(x => x.BuildPlan(It.IsAny<ProcessorRequest>(), It.IsAny<ProcessorContext>())).Returns(new ExecutionPlanResult { Candidates = [] });

        return CreateSut(
            store.Object,
            stateUpdater.Object,
            planner.Object,
            processor.Object,
            CreateProcessEventFactory().Object,
            outbox.Object,
            CreateObservability().Object,
            CreateCorrelationAccessor(caller: caller),
            transitionEvents);
    }

    private static Mock<IProcessorExecutor> ProcessorReturning(Func<ProcessorContext, ProcessExecutionResult> result)
    {
        var processor = new Mock<IProcessorExecutor>();
        processor
            .Setup(x => x.ExecuteAsync(It.IsAny<IReadOnlyCollection<StepCandidate>>(), It.IsAny<ProcessorContext>(), It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyCollection<StepCandidate>, ProcessorContext, CancellationToken>((_, context, _) => Task.FromResult(result(context)));

        return processor;
    }

    private static KaleidoOutboxEvent OutboxEvent(string eventType) =>
        new() { EventId = Guid.NewGuid(), EventType = eventType, Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow };

    private static readonly KaleidoCaller Alice = new() { Name = "alice", Id = "sub-alice" };

    [Fact]
    public async Task ExecuteAsync_ForANewProcess_TagsTheCallerAsCreatedByAndUpdatedBy()
    {
        var store = new Mock<IKaleidoContextStore>();
        SetupSave(store);
        ProcessorContext? executedWith = null;
        var processor = ProcessorReturning(context =>
        {
            executedWith = context;
            return CreateExecutionResult(context.ProcessId);
        });

        await CreateNewProcessSut(store, new Mock<IKaleidoEventWriter>(), processor, Alice).ExecuteAsync(new ProcessRequest { Processor = new ProcessorRequest() });

        Assert.Equal(Alice, executedWith!.CreatedBy);
        Assert.Equal(Alice, executedWith.UpdatedBy);
    }

    [Fact]
    public async Task ExecuteAsync_ForAnExistingProcess_TagsTheCallerAsUpdatedBy_AndKeepsCreatedBy()
    {
        var request = CreateRequest();
        var processId = Assert.IsType<Guid>(request.ProcessId);
        var bob = new KaleidoCaller { Name = "bob" };
        var existing = CreateContext(processId, "old-request") with { CreatedBy = bob, UpdatedBy = bob };
        var store = new Mock<IKaleidoContextStore>();
        store.Setup(x => x.LoadAsync(processId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var stateUpdater = new Mock<IProcessorStateUpdater>();
        stateUpdater.Setup(x => x.Reconcile(existing)).Returns(existing);
        var planner = new Mock<IProcessorPlanner>();
        planner.Setup(x => x.BuildPlan(It.IsAny<ProcessorRequest>(), It.IsAny<ProcessorContext>())).Returns(new ExecutionPlanResult { Candidates = [] });
        ProcessorContext? executedWith = null;
        var processor = ProcessorReturning(context =>
        {
            executedWith = context;
            return CreateExecutionResult(context.ProcessId);
        });

        await CreateSut(
                store.Object, stateUpdater.Object, planner.Object, processor.Object,
                CreateProcessEventFactory().Object, new Mock<IKaleidoEventWriter>().Object, CreateObservability().Object,
                CreateCorrelationAccessor(caller: Alice))
            .ExecuteAsync(request);

        Assert.Equal(bob, executedWith!.CreatedBy);
        Assert.Equal(Alice, executedWith.UpdatedBy);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsOnlyInformationResponseErrors_AsRejectedInformationResponses()
    {
        var request = CreateRequest();
        var processId = Assert.IsType<Guid>(request.ProcessId);
        var context = CreateContext(processId, "REQ-001");
        var informationCandidate = new StepCandidate
        {
            StepName = nameof(InfoStep),
            Status = StepCandidateStatus.Invalid,
            Registration = new ProcessStepRegistration(
                typeof(InfoStep),
                null,
                typeof(object),
                [],
                [],
                [],
                new RepeatableOptions(),
                new ProcessStepMetadata { Name = nameof(InfoStep), Description = "Info step.", Version = "1.0", DisplayName = "Info step" })
        };
        informationCandidate.AddError(ProcessorErrorCodes.InformationResponseUnanswered, "Question 'q1' has no answer.");
        informationCandidate.AddError(ProcessorErrorCodes.Required, "The Items field is required.");

        var store = new Mock<IKaleidoContextStore>();
        store.Setup(x => x.LoadAsync(processId, It.IsAny<CancellationToken>())).ReturnsAsync(context);
        var stateUpdater = new Mock<IProcessorStateUpdater>();
        stateUpdater.Setup(x => x.Reconcile(context)).Returns(context);
        var planner = new Mock<IProcessorPlanner>();
        planner.Setup(x => x.BuildPlan(It.IsAny<ProcessorRequest>(), It.IsAny<ProcessorContext>())).Returns(new ExecutionPlanResult { Candidates = [informationCandidate] });
        var observation = new Mock<IProcessorExecutionObservation>();
        var observability = new Mock<IProcessorObservability>();
        observability.Setup(x => x.BeginExecution(It.IsAny<ProcessorExecutionObservationDetails>())).Returns(observation.Object);

        await CreateSut(
                store.Object, stateUpdater.Object, planner.Object, ProcessorReturning(x => CreateExecutionResult(x.ProcessId)).Object,
                CreateProcessEventFactory().Object, new Mock<IKaleidoEventWriter>().Object, observability.Object,
                CreateCorrelationAccessor())
            .ExecuteAsync(request);

        observation.Verify(x => x.InformationResponseRejected(nameof(InfoStep), ProcessorErrorCodes.InformationResponseUnanswered), Times.Once);
        observation.Verify(x => x.InformationResponseRejected(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ForANewProcess_SavesItRightAwayWithItsProcessCreatedEvent()
    {
        var created = OutboxEvent(KaleidoEventTypes.ProcessCreated);
        var outbox = new Mock<IKaleidoEventWriter>();
        outbox.Setup(x => x.Prepare(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>())).Returns(created);
        var store = new Mock<IKaleidoContextStore>();
        SetupSave(store);
        var transitionEvents = new List<KaleidoOutboxEvent?>();
        ProcessorContext? executedWith = null;
        var processor = ProcessorReturning(context =>
        {
            executedWith = context;
            return CreateExecutionResult(context.ProcessId);
        });

        await CreateNewProcessSut(store, outbox, processor, transitionEvents: transitionEvents).ExecuteAsync(new ProcessRequest { Processor = new ProcessorRequest() });

        Assert.Equal(created, Assert.Single(transitionEvents));
        Assert.Equal(1, executedWith!.Version);
    }

    [Fact]
    public async Task ExecuteAsync_ForANewProcess_WithoutAPublisher_StillSavesTheProcess()
    {
        var store = new Mock<IKaleidoContextStore>();
        SetupSave(store);
        var transitionEvents = new List<KaleidoOutboxEvent?>();
        var outbox = new Mock<IKaleidoEventWriter>();

        await CreateNewProcessSut(store, outbox, ProcessorReturning(context => CreateExecutionResult(context.ProcessId)), transitionEvents: transitionEvents)
            .ExecuteAsync(new ProcessRequest { Processor = new ProcessorRequest() });

        Assert.Null(Assert.Single(transitionEvents));
        store.Verify(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EnqueuesPlanBuiltAndExecutionCompletedIndependentOfTheRequestCancellation()
    {
        var store = new Mock<IKaleidoContextStore>();
        SetupSave(store);
        var outbox = new Mock<IKaleidoEventWriter>();

        await CreateNewProcessSut(store, outbox, ProcessorReturning(context => CreateExecutionResult(context.ProcessId)))
            .ExecuteAsync(new ProcessRequest { Processor = new ProcessorRequest() }, TestContext.Current.CancellationToken);

        outbox.Verify(x => x.WriteAsync(It.IsAny<KaleidoEventEnvelope<PlanBuilt, ProcessEventContext>>(), CancellationToken.None), Times.Once);
        outbox.Verify(x => x.WriteAsync(It.IsAny<KaleidoEventEnvelope<ExecutionCompleted, ProcessEventContext>>(), CancellationToken.None), Times.Once);
    }

    private static ProcessEventContext CreateStubContext(Guid processId) =>
        new()
        {
            RequestId = Guid.NewGuid().ToString(),
            ServiceName = "test",
            ProcessId = processId,
            StepName = string.Empty
        };

    private static Mock<IProcessorEventFactory> CreateProcessEventFactory()
    {
        var factory =
            new Mock<IProcessorEventFactory>(MockBehavior.Strict);

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
        string requestId = "REQ-001",
        string? callingProcessor = null,
        KaleidoCaller? caller = null)
    {
        var accessor = new Mock<IKaleidoCorrelationContextAccessor>();

        accessor
            .SetupGet(x => x.Current)
            .Returns(new KaleidoCorrelationContext
            {
                RequestId = requestId,
                CallingProcessorName = callingProcessor,
                CallerName = caller?.Name,
                CallerId = caller?.Id,
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

    private sealed class InfoStep : IInformationStep
    {
        public string InformationRequestId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
