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
            new ProcessStepMetadata { Name = "test-step", Description = "desc", Version = "1.0", DisplayName = "display" });

        _registry.Setup(r => r.Registrations).Returns([registration]);

        var result = Sut.Initialize(processId);

        Assert.Equal(processId, result.ProcessId);
        Assert.Equal("test-service", result.ProcessorName);
        Assert.Equal(ProcessExecutionState.Active, result.State);
        Assert.Single(result.Steps);
        Assert.Equal("test-step", result.Steps.First().StepName);
        Assert.Equal("1.0", result.Steps.First().Version);
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
            new ProcessStepMetadata { Name = "new-step", Description = "desc", Version = "1.0", DisplayName = "display" });

        _registry.Setup(r => r.Registrations).Returns([newRegistration]);

        var result = Sut.Reconcile(context);

        Assert.Single(result.Steps);
        Assert.Equal("new-step", result.Steps.First().StepName);
        Assert.Equal(StepExecutionStatus.Pending, result.Steps.First().Status);
    }

    // ---------------------------------------------------------------------------
    // Versions across a deployment (ADR 0026): a step keeps the version it ran with; a step that has
    // not run follows the deployed definition.

    private static ProcessStepRegistration Registered(string name, string version, bool repeatable = false) =>
        new(
            typeof(object),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = repeatable },
            new ProcessStepMetadata { Name = name, Description = "desc", Version = version, DisplayName = name });

    [Fact]
    public void Reconcile_AStepThatHasNotRun_FollowsTheDeployedVersion()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [new StepContext { StepName = "test-step", Version = "1.0" }] };
        _registry.Setup(r => r.Registrations).Returns([Registered("test-step", "2.0")]);

        Assert.Equal("2.0", Sut.Reconcile(context).Steps.Single().Version);
    }

    [Fact]
    public void Reconcile_AStepThatRan_KeepsTheVersionItRanWith()
    {
        var ran = new StepContext { StepName = "test-step", Version = "1.0", Status = StepExecutionStatus.Completed, FirstExecuted = DateTimeOffset.UtcNow, LastExecuted = DateTimeOffset.UtcNow };
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [ran] };
        _registry.Setup(r => r.Registrations).Returns([Registered("test-step", "2.0")]);

        Assert.Equal("1.0", Sut.Reconcile(context).Steps.Single().Version);
    }

    // Each attempt records the version that ran: a repeatable step run again after the deployment.
    [Theory]
    [InlineData(ExecutionDecisionType.Continue)]
    [InlineData(ExecutionDecisionType.BusinessFailure)]
    public void ApplyExecution_RecordsTheVersionThatRan(ExecutionDecisionType decision)
    {
        var ran = new StepContext { StepName = "test-step", Version = "1.0", Status = StepExecutionStatus.Completed, LastExecuted = DateTimeOffset.UtcNow };
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [ran] };
        var candidate = new StepCandidate { StepName = "test-step", Registration = Registered("test-step", "2.0", repeatable: true) };

        Assert.Equal("2.0", Sut.ApplyExecution(context, candidate, new ExecutionDecision { Type = decision }).Steps.Single().Version);
    }

    [Fact]
    public void ApplyException_And_ApplyCancellation_RecordTheVersionThatRan()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [new StepContext { StepName = "test-step", Version = "1.0" }] };
        var candidate = new StepCandidate { StepName = "test-step", Registration = Registered("test-step", "2.0") };

        Assert.Equal("2.0", Sut.ApplyException(context, candidate).Steps.Single().Version);
        Assert.Equal("2.0", Sut.ApplyCancellation(context, candidate).Steps.Single().Version);
    }

    // A process started on 1.0 continues on the deployed 2.0 definition: history keeps what ran,
    // the next steps run (and are recorded) at 2.0, a new step appears, a removed one stays in history.
    [Fact]
    public void AProcessSpanningADeployment_KeepsItsHistory_AndContinuesOnTheDeployedDefinition()
    {
        _registry.Setup(r => r.Registrations).Returns([Registered("capture", "1.0"), Registered("review", "1.0"), Registered("legacy", "1.0")]);
        var started = Sut.ApplyExecution(
            Sut.Initialize(Guid.NewGuid()),
            new StepCandidate { StepName = "capture", Registration = Registered("capture", "1.0") },
            new ExecutionDecision { Type = ExecutionDecisionType.Continue });

        // Deployment: capture and review move to 2.0, legacy is removed, submit is new.
        _registry.Setup(r => r.Registrations).Returns([Registered("capture", "2.0"), Registered("review", "2.0"), Registered("submit", "2.0")]);
        var resumed = Sut.Reconcile(started);
        var reviewed = Sut.ApplyExecution(resumed, new StepCandidate { StepName = "review", Registration = Registered("review", "2.0") }, new ExecutionDecision { Type = ExecutionDecisionType.Continue });

        var versions = reviewed.Steps.ToDictionary(x => x.StepName, x => (x.Version, x.Status), StringComparer.Ordinal);
        Assert.Equal(("1.0", StepExecutionStatus.Completed), versions["capture"]);
        Assert.Equal(("2.0", StepExecutionStatus.Completed), versions["review"]);
        Assert.Equal(("2.0", StepExecutionStatus.Pending), versions["submit"]);
        Assert.Equal(("1.0", StepExecutionStatus.Pending), versions["legacy"]);
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
                new StepContext { StepName = "test-step", Version = "1.0" }
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
        Steps = [step ?? new StepContext { StepName = "test-step", Version = "1.0" }],
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
        var started = new StepContext { StepName = "test-step", Version = "1.0", FirstExecuted = firstExecuted, StartedBy = Alice, LastExecuted = firstExecuted, LastUpdatedBy = Alice };

        var result = Sut.ApplyExecution(RanBy(Bob, started), new StepCandidate { StepName = "test-step" }, new ExecutionDecision { Type = ExecutionDecisionType.Continue });

        var step = result.Steps.Single();
        Assert.Equal(Alice, step.StartedBy);
        Assert.Equal(firstExecuted, step.FirstExecuted);
        Assert.Equal(Bob, step.LastUpdatedBy);
        Assert.True(step.LastExecuted > firstExecuted);
    }

    [Fact]
    public void ApplyExceptionAndCancellation_TagTheCallerToo()
    {
        var candidate = new StepCandidate { StepName = "test-step" };

        var failed = Sut.ApplyException(RanBy(Alice), candidate).Steps.Single();
        var canceled = Sut.ApplyCancellation(RanBy(Bob), candidate).Steps.Single();

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
                new StepContext { StepName = "test-step", Version = "1.0" }
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
    [InlineData(ExecutionDecisionType.ProcessViolation, ProcessExecutionState.ProcessViolation)]
    [InlineData(ExecutionDecisionType.AwaitingRequiredStep, ProcessExecutionState.AwaitingRequiredStep)]
    [InlineData(ExecutionDecisionType.AwaitingStepSelection, ProcessExecutionState.AwaitingStepSelection)]
    [InlineData(ExecutionDecisionType.HandOff, ProcessExecutionState.HandOff)]
    public void ApplyExecution_MapsEachDecisionToItsState(ExecutionDecisionType decisionType, ProcessExecutionState expected)
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [new StepContext { StepName = "test-step", Version = "1.0" }] };

        var result = Sut.ApplyExecution(context, new StepCandidate { StepName = "test-step" }, new ExecutionDecision { Type = decisionType });

        Assert.Equal(expected, result.State);
    }

    [Fact]
    public void ApplyExecution_ForADecisionWithoutAProcessState_ThrowsAFrameworkException()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test", Steps = [new StepContext { StepName = "test-step", Version = "1.0" }] };

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
                new StepContext { StepName = "test-step", Version = "1.0" }
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
            Steps = [new StepContext { StepName = "test-step", Version = "1.0" }]
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
            Steps = [new StepContext { StepName = "info-step", Version = "1.0" }]
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
    public void ApplyException_KeepsThePendingRequest()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            RequiredStep = "info-step",
            RequiredInformationRequest = new InformationRequest { InformationRequestId = "out-of-network" },
            Steps = [new StepContext { StepName = "info-step", Version = "1.0" }]
        };

        var result = Sut.ApplyException(context, new StepCandidate { StepName = "info-step" });

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
                new StepContext { StepName = "test-step", Version = "1.0" }
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
    public void ApplyException_WhenContextIsNull_Throws()
    {
        var candidate = new StepCandidate { StepName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyException(null!, candidate));
    }

    [Fact]
    public void ApplyException_WhenCandidateIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyException(context, null!));
    }

    [Fact]
    public void ApplyException_WhenStepNotFound_Throws()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = []
        };
        var candidate = new StepCandidate { StepName = "unknown-step" };

        Assert.Throws<KaleidoFrameworkException>(() =>
            Sut.ApplyException(context, candidate));
    }

    [Fact]
    public void ApplyException_SetsStepStatusToException()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step", Version = "1.0" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyException(context, candidate);

        Assert.Equal(StepExecutionStatus.Exception, result.Steps.First().Status);
    }

    [Fact]
    public void ApplyException_KeepsTheProcessState()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            State = ProcessExecutionState.AwaitingStepSelection,
            Steps =
            [
                new StepContext { StepName = "test-step", Version = "1.0" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyException(context, candidate);

        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
    }

    [Fact]
    public void ApplyException_KeepsTheNextSteps()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step", Version = "1.0" }
            ],
            RequiredStep = "required",
            TargetProcessorName = "target",
            AvailableSteps = ["step1"]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyException(context, candidate);

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
            Steps = [new StepContext { StepName = "test-step", Version = "1.0" }]
        };

        var result = Sut.ApplyExecution(context, new StepCandidate { StepName = "test-step" }, ExecutionDecision.BusinessFailure());

        Assert.Equal(StepExecutionStatus.Failed, result.Steps.Single().Status);
        Assert.Equal(ProcessExecutionState.BusinessFailure, result.State);
    }

    [Fact]
    public void ApplyCancellation_WhenContextIsNull_Throws()
    {
        var candidate = new StepCandidate { StepName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyCancellation(null!, candidate));
    }

    [Fact]
    public void ApplyCancellation_WhenCandidateIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.ApplyCancellation(context, null!));
    }

    [Fact]
    public void ApplyCancellation_WhenStepNotFound_Throws()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = []
        };
        var candidate = new StepCandidate { StepName = "unknown-step" };

        Assert.Throws<KaleidoFrameworkException>(() =>
            Sut.ApplyCancellation(context, candidate));
    }

    [Fact]
    public void ApplyCancellation_SetsStepStatusToCanceled()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step", Version = "1.0" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyCancellation(context, candidate);

        Assert.Equal(StepExecutionStatus.Canceled, result.Steps.First().Status);
    }

    [Fact]
    public void ApplyCancellation_SetsStateToCancelled()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step", Version = "1.0" }
            ]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyCancellation(context, candidate);

        Assert.Equal(ProcessExecutionState.Canceled, result.State);
    }

    [Fact]
    public void ApplyCancellation_ClearsStateProperties()
    {
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps =
            [
                new StepContext { StepName = "test-step", Version = "1.0" }
            ],
            RequiredStep = "required",
            TargetProcessorName = "target",
            AvailableSteps = ["step1"]
        };
        var candidate = new StepCandidate { StepName = "test-step" };

        var result = Sut.ApplyCancellation(context, candidate);

        Assert.Null(result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Empty(result.AvailableSteps);
    }
}
