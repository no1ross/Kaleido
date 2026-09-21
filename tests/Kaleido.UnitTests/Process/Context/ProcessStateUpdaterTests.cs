using Kaleido.Exceptions;
using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;
using Moq;
using Xunit;

namespace Kaleido.Process.UnitTests.Context;

public sealed class ProcessStateUpdaterTests
{
    private readonly Mock<IProcessStepRegistry> _registry = new();
    private readonly KaleidoServiceOptions _options = new() { ServiceName = "test-service" };
    private readonly ProcessStateUpdater _updater;

    public ProcessStateUpdaterTests()
    {
        _updater = new ProcessStateUpdater(_registry.Object, _options);
    }

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
            new ProcessStepMetadata("test-step", "desc", "1.0", "display"));

        _registry.Setup(r => r.Registrations).Returns([registration]);

        var result = _updater.Initialize(processId);

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
        var result = _updater.Initialize(processId);
        var after = DateTime.UtcNow;

        Assert.InRange(result.CreatedUtc, before, after);
        Assert.InRange(result.UpdatedUtc, before, after);
    }

    [Fact]
    public void Reconcile_WhenContextIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _updater.Reconcile(null!));
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
            new ProcessStepMetadata("new-step", "desc", "1.0", "display"));

        _registry.Setup(r => r.Registrations).Returns([newRegistration]);

        var result = _updater.Reconcile(context);

        Assert.Single(result.Steps);
        Assert.Equal("new-step", result.Steps.First().StepName);
        Assert.Equal(StepExecutionStatus.Pending, result.Steps.First().Status);
    }

    [Fact]
    public void Reconcile_UpdatesExistingStepVersion()
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

        var updatedRegistration = new ProcessStepRegistration(
            typeof(object),
            typeof(object),
            typeof(object),
            [],
            [],
            [],
            new RepeatableOptions { Enabled = false },
            new ProcessStepMetadata("test-step", "desc", "2.0", "display"));

        _registry.Setup(r => r.Registrations).Returns([updatedRegistration]);

        var result = _updater.Reconcile(context);

        Assert.Single(result.Steps);
        Assert.Equal("2.0", result.Steps.First().Version);
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
        var result = _updater.Reconcile(context);
        var after = DateTime.UtcNow;

        Assert.InRange(result.UpdatedUtc, before, after);
    }

    [Fact]
    public void ApplyExecution_WhenContextIsNull_Throws()
    {
        var candidate = new StepCandidate { StepName = "test" };
        var decision = new ExecutionDecision { Type = ExecutionDecisionType.Continue };

        Assert.Throws<ArgumentNullException>(() =>
            _updater.ApplyExecution(null!, candidate, decision));
    }

    [Fact]
    public void ApplyExecution_WhenCandidateIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var decision = new ExecutionDecision { Type = ExecutionDecisionType.Continue };

        Assert.Throws<ArgumentNullException>(() =>
            _updater.ApplyExecution(context, null!, decision));
    }

    [Fact]
    public void ApplyExecution_WhenDecisionIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var candidate = new StepCandidate { StepName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            _updater.ApplyExecution(context, candidate, null!));
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
            _updater.ApplyExecution(context, candidate, decision));
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

        var result = _updater.ApplyExecution(context, candidate, decision);

        Assert.Equal(StepExecutionStatus.Completed, result.Steps.First().Status);
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

        var result = _updater.ApplyExecution(context, candidate, decision);

        Assert.Equal(ProcessExecutionState.Complete, result.State);
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

        var result = _updater.ApplyExecution(context, candidate, decision);

        Assert.Equal("required-step", result.RequiredStep);
        Assert.Equal("target-processor", result.TargetProcessorName);
        Assert.Equal(2, result.AvailableSteps.Count);
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
        var result = _updater.ApplyExecution(context, candidate, decision);
        var after = DateTime.UtcNow;

        Assert.InRange(result.UpdatedUtc, before, after);
    }

    [Fact]
    public void ApplyException_WhenContextIsNull_Throws()
    {
        var candidate = new StepCandidate { StepName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            _updater.ApplyException(null!, candidate));
    }

    [Fact]
    public void ApplyException_WhenCandidateIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            _updater.ApplyException(context, null!));
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
            _updater.ApplyException(context, candidate));
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

        var result = _updater.ApplyException(context, candidate);

        Assert.Equal(StepExecutionStatus.Exception, result.Steps.First().Status);
    }

    [Fact]
    public void ApplyException_SetsStateToException()
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

        var result = _updater.ApplyException(context, candidate);

        Assert.Equal(ProcessExecutionState.Exception, result.State);
    }

    [Fact]
    public void ApplyException_ClearsStateProperties()
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

        var result = _updater.ApplyException(context, candidate);

        Assert.Null(result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Empty(result.AvailableSteps);
    }

    [Fact]
    public void ApplyCancellation_WhenContextIsNull_Throws()
    {
        var candidate = new StepCandidate { StepName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            _updater.ApplyCancellation(null!, candidate));
    }

    [Fact]
    public void ApplyCancellation_WhenCandidateIsNull_Throws()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            _updater.ApplyCancellation(context, null!));
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
            _updater.ApplyCancellation(context, candidate));
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

        var result = _updater.ApplyCancellation(context, candidate);

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

        var result = _updater.ApplyCancellation(context, candidate);

        Assert.Equal(ProcessExecutionState.Cancelled, result.State);
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

        var result = _updater.ApplyCancellation(context, candidate);

        Assert.Null(result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Empty(result.AvailableSteps);
    }
}
