using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Processor.Registry;

using Kaleido.UnitTests;

namespace Kaleido.Processor.Context.UnitTests;

public sealed class ProcessorStateUpdaterTests
    : SutFixture
{
    private readonly Mock<IProcessorStepRegistry> _registry = new();
    private readonly KaleidoServiceOptions _options = new() { ServiceName = "test-service" };

    private ProcessorStateUpdater Sut => CreateSut();

    private ProcessorStateUpdater CreateSut() =>
        new(_registry.Object, _options);

    [Fact]
    public void Initialize_CreatesContextWithCorrectProperties()
    {
        var processId = Guid.NewGuid();
        var registration = new ProcessStepRegistration(
            typeof(object),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "test-step", Description = "desc", DisplayName = "display" });

        _registry.Setup(r => r.Registrations).Returns([registration]);

        var result = Sut.Initialize(processId);

        Assert.Equal(processId, result.ProcessId);
        Assert.Equal("test-service", result.ProcessorName);
        Assert.Equal(ProcessExecutionState.Active, result.State);
        Assert.Single(result.Steps);
        Assert.Equal("test-step", result.Steps.First().StepName);
        Assert.Equal(StepExecutionStatus.Pending, result.Steps.First().Status);
    }

    [Fact]
    public void Initialize_SetsTimestamps()
    {
        var processId = Guid.NewGuid();
        _registry.Setup(r => r.Registrations).Returns([]);

        var before = DateTime.UtcNow;
        var result = Sut.Initialize(processId);
        var after = DateTime.UtcNow;

        Assert.InRange(result.CreatedUtc, before, after);
        Assert.InRange(result.UpdatedUtc, before, after);
    }

    [Fact]
    public void Reconcile_WhenContextIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Sut.Reconcile(null!));
    }

    [Fact]
    public void Reconcile_AddsNewSteps()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = []
        };

        var newRegistration = new ProcessStepRegistration(
            typeof(object),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = "new-step", Description = "desc", DisplayName = "display" });

        _registry.Setup(r => r.Registrations).Returns([newRegistration]);

        var result = Sut.Reconcile(context);

        Assert.Single(result.Steps);
        Assert.Equal("new-step", result.Steps.First().StepName);
        Assert.Equal(StepExecutionStatus.Pending, result.Steps.First().Status);
    }

    // ---------------------------------------------------------------------------
    // A deployment (ADR 0026, ADR 0029): a process continues on the deployed definition.

    private static ProcessStepRegistration Registered(string name) =>
        new(
            typeof(object),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata { Name = name, Description = "desc", DisplayName = name });

    // History keeps what ran, the next step runs on the deployed definition, a new step appears and
    // a removed one stays in history.
    [Fact]
    public void AProcessSpanningADeployment_KeepsItsHistory_AndContinuesOnTheDeployedDefinition()
    {
        _registry.Setup(r => r.Registrations).Returns([Registered("capture"), Registered("review"), Registered("legacy")]);
        var started = Sut.ApplyExecution(
            Sut.Initialize(Guid.NewGuid()),
            new StepCandidate { StepName = "capture", Registration = Registered("capture") },
            new ExecutionDecision { Type = ExecutionDecisionType.Continue });

        // Deployment: legacy is removed, submit is new.
        _registry.Setup(r => r.Registrations).Returns([Registered("capture"), Registered("review"), Registered("submit")]);
        var resumed = Sut.Reconcile(started);
        var reviewed = Sut.ApplyExecution(resumed, new StepCandidate { StepName = "review", Registration = Registered("review") }, new ExecutionDecision { Type = ExecutionDecisionType.Continue });

        var statuses = reviewed.Steps.ToDictionary(x => x.StepName, x => x.Status, StringComparer.Ordinal);
        Assert.Equal(StepExecutionStatus.Completed, statuses["capture"]);
        Assert.Equal(StepExecutionStatus.Completed, statuses["review"]);
        Assert.Equal(StepExecutionStatus.Pending, statuses["submit"]);
        Assert.Equal(StepExecutionStatus.Pending, statuses["legacy"]);
    }

    // A step already in the process is not added again.
    [Fact]
    public void Reconcile_KeepsAKnownStepAsItIs()
    {
        var ran = new StepContext { StepName = "test-step", Status = StepExecutionStatus.Completed, LastExecuted = DateTimeOffset.UtcNow };
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [ran] };
        _registry.Setup(r => r.Registrations).Returns([Registered("TEST-STEP")]);

        Assert.Equal(ran, Assert.Single(Sut.Reconcile(context).Steps));
    }

    [Fact]
    public void Reconcile_UpdatesTimestamp()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = [],
            UpdatedUtc = DateTime.UtcNow.AddDays(-1)
        };

        _registry.Setup(r => r.Registrations).Returns([]);

        var before = DateTime.UtcNow;
        var result = Sut.Reconcile(context);
        var after = DateTime.UtcNow;

        Assert.InRange(result.UpdatedUtc, before, after);
    }

    [Fact]
    public void ApplyExecution_WhenContextIsNull_Throws()
    {
        var candidate = new StepCandidate { StepName = "test" };
        var decision = new ExecutionDecision { Type = ExecutionDecisionType.Continue };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyExecution(null!, candidate, decision));
    }

    [Fact]
    public void ApplyExecution_WhenCandidateIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var decision = new ExecutionDecision { Type = ExecutionDecisionType.Continue };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyExecution(context, null!, decision));
    }

    [Fact]
    public void ApplyExecution_WhenDecisionIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var candidate = new StepCandidate { StepName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyExecution(context, candidate, null!));
    }

    [Fact]
    public void ApplyExecution_WhenStepNotFound_Throws()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = []
        };
        var candidate = new StepCandidate { StepName = "unknown-step" };
        var decision = new ExecutionDecision { Type = ExecutionDecisionType.Continue };

        Assert.Throws<KaleidoFrameworkException>(() =>
            Sut.ApplyExecution(context, candidate, decision));
    }

    [Fact]
    public void ApplyExecution_UpdatesStepStatusToCompleted()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };
        var decision = new ExecutionDecision { Type = ExecutionDecisionType.Continue };

        var result = Sut.ApplyExecution(context, candidate, decision);

        Assert.Equal(StepExecutionStatus.Completed, result.Steps.First().Status);
    }

    private static readonly KaleidoCaller Alice = new() { Name = "alice", Id = "sub-alice" };
    private static readonly KaleidoCaller Bob = new() { Name = "bob", Id = "sub-bob" };

    private static ProcessorContext RanBy(KaleidoCaller? caller, StepContext? step = null) => new()
    {
        ProcessId = Guid.NewGuid(),
        ProcessorName = "test",
        UpdatedBy = caller,
        Steps = [step ?? new StepContext { StepName = "test-step" }],
    };

    [Fact]
    public void ApplyExecution_TheFirstAttempt_TagsStartedByAndLastUpdatedBy_WithBothTimes()
    {
        var result = Sut.ApplyExecution(RanBy(Alice), new StepCandidate { StepName = "test-step" }, new ExecutionDecision { Type = ExecutionDecisionType.Continue });

        var step = result.Steps.Single();
        Assert.Equal(Alice, step.StartedBy);
        Assert.Equal(Alice, step.LastUpdatedBy);
        Assert.NotNull(step.FirstExecuted);
        Assert.Equal(step.FirstExecuted, step.LastExecuted);
    }

    [Fact]
    public void ApplyExecution_ALaterAttempt_KeepsWhoStartedTheStepAndWhen()
    {
        var firstExecuted = DateTimeOffset.UtcNow.AddHours(-1);
        var started = new StepContext { StepName = "test-step", FirstExecuted = firstExecuted, StartedBy = Alice, LastExecuted = firstExecuted, LastUpdatedBy = Alice };

        var result = Sut.ApplyExecution(RanBy(Bob, started), new StepCandidate { StepName = "test-step" }, new ExecutionDecision { Type = ExecutionDecisionType.Continue });

        var step = result.Steps.Single();
        Assert.Equal(Alice, step.StartedBy);
        Assert.Equal(firstExecuted, step.FirstExecuted);
        Assert.Equal(Bob, step.LastUpdatedBy);
        Assert.True(step.LastExecuted > firstExecuted);
    }

    [Fact]
    public void ApplyUnfinishedStep_TagsTheCallerToo()
    {
        var candidate = new StepCandidate { StepName = "test-step" };

        var failed = Sut.ApplyUnfinishedStep(RanBy(Alice), candidate, StepExecutionStatus.Exception).Steps.Single();
        var canceled = Sut.ApplyUnfinishedStep(RanBy(Bob), candidate, StepExecutionStatus.Canceled).Steps.Single();

        Assert.Equal(Alice, failed.StartedBy);
        Assert.Equal(Alice, failed.LastUpdatedBy);
        Assert.Equal(Bob, canceled.StartedBy);
        Assert.Equal(Bob, canceled.LastUpdatedBy);
    }

    [Fact]
    public void ApplyExecution_ForAnAnonymousCaller_TagsNoCaller_ButRecordsTheTimes()
    {
        var step = Sut.ApplyExecution(RanBy(null), new StepCandidate { StepName = "test-step" }, new ExecutionDecision { Type = ExecutionDecisionType.Continue }).Steps.Single();

        Assert.Null(step.StartedBy);
        Assert.Null(step.LastUpdatedBy);
        Assert.NotNull(step.FirstExecuted);
    }

    [Fact]
    public void ApplyExecution_MapsDecisionToState()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };
        var decision = new ExecutionDecision { Type = ExecutionDecisionType.Complete };

        var result = Sut.ApplyExecution(context, candidate, decision);

        Assert.Equal(ProcessExecutionState.Complete, result.State);
    }

    [Theory]
    [InlineData(ExecutionDecisionType.Continue, ProcessExecutionState.Active)]
    [InlineData(ExecutionDecisionType.BusinessFailure, ProcessExecutionState.BusinessFailure)]
    [InlineData(ExecutionDecisionType.AwaitingRequiredStep, ProcessExecutionState.AwaitingRequiredStep)]
    [InlineData(ExecutionDecisionType.AwaitingStepSelection, ProcessExecutionState.AwaitingStepSelection)]
    [InlineData(ExecutionDecisionType.HandOff, ProcessExecutionState.HandOff)]
    public void ApplyExecution_MapsEachDecisionToItsState(ExecutionDecisionType decisionType, ProcessExecutionState expected)
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [new StepContext { StepName = "test-step" }] };

        var result = Sut.ApplyExecution(context, new StepCandidate { StepName = "test-step" }, new ExecutionDecision { Type = decisionType });

        Assert.Equal(expected, result.State);
    }

    // A process violation is the handler's defect: like an exception, the process keeps its state and
    // next steps, and only the step's status records it (#522).
    [Fact]
    public void ApplyExecution_ForAProcessViolation_KeepsTheProcessAndRecordsTheStepAsAnException()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            State = ProcessExecutionState.AwaitingStepSelection,
            AvailableSteps = ["test-step", "other-step"],
            Steps = [new StepContext { StepName = "test-step" }],
        };

        var result = Sut.ApplyExecution(
            context,
            new StepCandidate { StepName = "test-step" },
            new ExecutionDecision { Type = ExecutionDecisionType.ProcessViolation, RequiredStep = "ignored", TargetProcessorName = "ignored" });

        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
        Assert.Equal(["test-step", "other-step"], result.AvailableSteps);
        Assert.Null(result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Equal(StepExecutionStatus.Exception, Assert.Single(result.Steps).Status);
    }

    [Fact]
    public void ApplyExecution_ForADecisionWithoutAProcessState_ThrowsAFrameworkException()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [new StepContext { StepName = "test-step" }] };

        var exception =
            Assert.Throws<Kaleido.Exceptions.KaleidoFrameworkException>(() =>
                Sut.ApplyExecution(context, new StepCandidate { StepName = "test-step" }, new ExecutionDecision { Type = ExecutionDecisionType.Rejected }));

        Assert.Equal(Kaleido.Exceptions.FrameworkErrorCodes.TypeMismatch, exception.Code);
        Assert.Contains("Rejected", exception.Message);
    }

    [Fact]
    public void ApplyExecution_SetsDecisionProperties()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };
        var decision = new ExecutionDecision
        {
            Type = ExecutionDecisionType.AwaitingRequiredStep,
            RequiredStep = "required-step",
            TargetProcessorName = "target-processor",
            AvailableSteps = ["step1", "step2"]
        };

        var result = Sut.ApplyExecution(context, candidate, decision);

        Assert.Equal("required-step", result.RequiredStep);
        Assert.Equal("target-processor", result.TargetProcessorName);
        Assert.Equal(2, result.AvailableSteps.Count);
    }

    [Fact]
    public void ApplyExecution_AwaitingInformation_StoresThePendingRequest()
    {
        var request = new InformationRequest { InformationRequestId = "out-of-network" };
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = [new StepContext { StepName = "test-step" }]
        };
        var decision = ExecutionDecision.AwaitingInformation("info-step", request);

        var result = Sut.ApplyExecution(context, new StepCandidate { StepName = "test-step" }, decision);

        Assert.Equal(ProcessExecutionState.AwaitingInformation, result.State);
        Assert.Equal("info-step", result.RequiredStep);
        Assert.Same(request, result.RequiredInformationRequest);
    }

    [Fact]
    public void ApplyExecution_NextOutcomeWithoutRequest_ClearsThePendingRequest()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            RequiredStep = "info-step",
            RequiredInformationRequest = new InformationRequest { InformationRequestId = "out-of-network" },
            Steps = [new StepContext { StepName = "info-step" }]
        };

        var result =
            Sut.ApplyExecution(
                context,
                new StepCandidate { StepName = "info-step" },
                ExecutionDecision.AwaitingStepSelection(["next-step"]));

        Assert.Null(result.RequiredStep);
        Assert.Null(result.RequiredInformationRequest);
    }

    [Fact]
    public void ApplyUnfinishedStep_KeepsThePendingRequest()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            RequiredStep = "info-step",
            RequiredInformationRequest = new InformationRequest { InformationRequestId = "out-of-network" },
            Steps = [new StepContext { StepName = "info-step" }]
        };

        var result = Sut.ApplyUnfinishedStep(context, new StepCandidate { StepName = "info-step" }, StepExecutionStatus.Exception);

        Assert.Equal("out-of-network", result.RequiredInformationRequest?.InformationRequestId);
    }

    [Fact]
    public void ApplyExecution_UpdatesTimestamp()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ],
            UpdatedUtc = DateTime.UtcNow.AddDays(-1)
        };
        var candidate = new StepCandidate { StepName = "test-step" };
        var decision = new ExecutionDecision { Type = ExecutionDecisionType.Continue };

        var before = DateTime.UtcNow;
        var result = Sut.ApplyExecution(context, candidate, decision);
        var after = DateTime.UtcNow;

        Assert.InRange(result.UpdatedUtc, before, after);
    }

    [Fact]
    public void ApplyUnfinishedStep_WhenContextIsNull_Throws()
    {
        var candidate = new StepCandidate { StepName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyUnfinishedStep(null!, candidate, StepExecutionStatus.Exception));
    }

    [Fact]
    public void ApplyUnfinishedStep_WhenCandidateIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyUnfinishedStep(context, null!, StepExecutionStatus.Exception));
    }

    [Fact]
    public void ApplyUnfinishedStep_WhenStepNotFound_Throws()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = []
        };
        var candidate = new StepCandidate { StepName = "unknown-step" };

        Assert.Throws<KaleidoFrameworkException>(() =>
            Sut.ApplyUnfinishedStep(context, candidate, StepExecutionStatus.Exception));
    }

    [Fact]
    public void ApplyUnfinishedStep_SetsStepStatusToException()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyUnfinishedStep(context, candidate, StepExecutionStatus.Exception);

        Assert.Equal(StepExecutionStatus.Exception, result.Steps.First().Status);
    }

    [Fact]
    public void ApplyUnfinishedStep_KeepsTheProcessState()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            State = ProcessExecutionState.AwaitingStepSelection,
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyUnfinishedStep(context, candidate, StepExecutionStatus.Exception);

        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
    }

    [Fact]
    public void ApplyUnfinishedStep_KeepsTheNextSteps()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ],
            RequiredStep = "required",
            TargetProcessorName = "target",
            AvailableSteps = ["step1"]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyUnfinishedStep(context, candidate, StepExecutionStatus.Exception);

        Assert.Equal("required", result.RequiredStep);
        Assert.Equal("target", result.TargetProcessorName);
        Assert.Equal(["step1"], result.AvailableSteps);
    }

    [Fact]
    public void ApplyExecution_BusinessFailure_RecordsTheStepAsFailed()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = [new StepContext { StepName = "test-step" }]
        };

        var result = Sut.ApplyExecution(context, new StepCandidate { StepName = "test-step" }, ExecutionDecision.BusinessFailure());

        Assert.Equal(StepExecutionStatus.Failed, result.Steps.Single().Status);
        Assert.Equal(ProcessExecutionState.BusinessFailure, result.State);
    }

    [Fact]
    public void ApplyUnfinishedStep_SetsStepStatusToCanceled()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyUnfinishedStep(context, candidate, StepExecutionStatus.Canceled);

        Assert.Equal(StepExecutionStatus.Canceled, result.Steps.First().Status);
    }

    // A cancellation is a step status only (#517, #536): the process keeps its state and next steps, so
    // the step can be sent again.
    [Theory]
    [InlineData(StepExecutionStatus.Canceled)]
    [InlineData(StepExecutionStatus.ValidationFailed)]
    [InlineData(StepExecutionStatus.Exception)]
    public void ApplyUnfinishedStep_KeepsTheProcessAndRecordsOnlyTheStepStatus(StepExecutionStatus status)
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            State = ProcessExecutionState.AwaitingRequiredStep,
            Steps =
            [
                new StepContext { StepName = "test-step" }
            ],
            RequiredStep = "required",
            TargetProcessorName = "target",
            AvailableSteps = ["step1"]
        };

        var result = Sut.ApplyUnfinishedStep(context, new StepCandidate { StepName = "test-step" }, status);

        Assert.Equal(ProcessExecutionState.AwaitingRequiredStep, result.State);
        Assert.Equal("required", result.RequiredStep);
        Assert.Equal("target", result.TargetProcessorName);
        Assert.Equal(["step1"], result.AvailableSteps);
        Assert.Equal(status, Assert.Single(result.Steps).Status);
    }

    // Completed and failed steps change the process: they go through ApplyExecution, never here.
    [Theory]
    [InlineData(StepExecutionStatus.Completed)]
    [InlineData(StepExecutionStatus.Failed)]
    [InlineData(StepExecutionStatus.Rejected)]
    [InlineData(StepExecutionStatus.Pending)]
    public void ApplyUnfinishedStep_ForAStatusOfAFinishedStep_ThrowsAFrameworkException(StepExecutionStatus status)
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [new StepContext { StepName = "test-step" }] };

        var exception =
            Assert.Throws<Kaleido.Exceptions.KaleidoFrameworkException>(() =>
                Sut.ApplyUnfinishedStep(context, new StepCandidate { StepName = "test-step" }, status));

        Assert.Equal(Kaleido.Exceptions.FrameworkErrorCodes.TypeMismatch, exception.Code);
        Assert.Contains(status.ToString(), exception.Message);
    }
}
