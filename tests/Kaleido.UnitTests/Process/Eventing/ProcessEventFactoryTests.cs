using Kaleido.Observability;
using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Registry;

namespace Kaleido.Process.UnitTests.Eventing;

public sealed class ProcessEventFactoryTests
{
    private readonly KaleidoServiceOptions _options = new() { ServiceName = "test-service" };
    private readonly ProcessEventFactory _factory;

    public ProcessEventFactoryTests()
    {
        _factory = new ProcessEventFactory(_options);
    }

    [Fact]
    public void CreateProcessCreated_WhenContextIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateProcessCreated(correlation, null!, request));
    }

    [Fact]
    public void CreateProcessCreated_WhenRequestIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateProcessCreated(correlation, context, null!));
    }

    [Fact]
    public void CreateProcessCreated_CreatesEventWithCorrectContext()
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request" };
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            State = ProcessExecutionState.Active
        };
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };

        var result = _factory.CreateProcessCreated(correlation, context, request);

        Assert.NotNull(result);
        Assert.NotNull(result.Context);
        Assert.Equal("test-request", result.Context.RequestId);
        Assert.Equal("test-service", result.Context.ServiceName);
        Assert.Equal(context.ProcessId, result.Context.ProcessId);
        Assert.NotNull(result.Event);
        Assert.Equal(ProcessExecutionState.Active, result.Event.State);
    }

    [Fact]
    public void CreateProcessCreated_ExtractsStepNamesFromRequest()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var request = new ProcessRequest
        {
            Processor = new ProcessorRequest
            {
                Steps = new Dictionary<string, object?>
                {
                    ["step1"] = new { },
                    ["step2"] = new { }
                }
            }
        };

        var result = _factory.CreateProcessCreated(correlation, context, request);

        Assert.Equal(2, result.Event.SubmittedStepCount);
        Assert.Contains("step1", result.Event.SubmittedStepNames);
        Assert.Contains("step2", result.Event.SubmittedStepNames);
    }

    [Fact]
    public void CreatePlanBuilt_WhenContextIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };
        var plan = new ExecutionPlanResult { Candidates = [] };

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreatePlanBuilt(correlation, null!, request, plan, 0));
    }

    [Fact]
    public void CreatePlanBuilt_WhenRequestIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var plan = new ExecutionPlanResult { Candidates = [] };

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreatePlanBuilt(correlation, context, null!, plan, 0));
    }

    [Fact]
    public void CreatePlanBuilt_WhenPlanIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreatePlanBuilt(correlation, context, request, null!, 0));
    }

    [Fact]
    public void CreatePlanBuilt_CreatesEventWithCandidates()
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request" };
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };

        var candidate = new StepCandidate
        {
            StepName = "test-step",
            Status = StepCandidateStatus.Built,
            Registration = new ProcessStepRegistration(
                typeof(object),
                typeof(object),
                typeof(object),
                [],
                [],
                [],
                new RepeatableOptions { Enabled = false },
                new ProcessStepMetadata("test-step", "desc", "1.0", "display")),
            IncludedInExecutionPlan = true
        };

        var plan = new ExecutionPlanResult { Candidates = [candidate] };

        var result = _factory.CreatePlanBuilt(correlation, context, request, plan, 1);

        Assert.NotNull(result);
        Assert.NotNull(result.Event);
        Assert.Single(result.Event.Candidates);
        Assert.Equal("test-step", result.Event.Candidates.First().StepName);
        Assert.Equal("1.0", result.Event.Candidates.First().StepVersion);
        Assert.Equal(StepCandidateStatus.Built, result.Event.Candidates.First().CandidateStatus);
        Assert.True(result.Event.Candidates.First().IncludedInExecutionPlan);
    }

    [Fact]
    public void CreateStepCompleted_WhenContextIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var candidate = new StepCandidate { StepName = "test" };
        var outcome = new ProcessExecutionOutcome
        {
            StepName = "test",
            Status = StepExecutionStatus.Completed,
            Outcome = StepExecutionOutcome.Completed,
            Decision = ExecutionDecisionType.Continue
        };
        var result = new ProcessStepInvokerResult();

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateStepCompleted(correlation, null!, candidate, outcome, result));
    }

    [Fact]
    public void CreateStepCompleted_WhenCandidateIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var outcome = new ProcessExecutionOutcome
        {
            StepName = "test",
            Status = StepExecutionStatus.Completed,
            Outcome = StepExecutionOutcome.Completed,
            Decision = ExecutionDecisionType.Continue
        };
        var result = new ProcessStepInvokerResult();

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateStepCompleted(correlation, context, null!, outcome, result));
    }

    [Fact]
    public void CreateStepCompleted_WhenOutcomeIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var candidate = new StepCandidate { StepName = "test" };
        var result = new ProcessStepInvokerResult();

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateStepCompleted(correlation, context, candidate, null!, result));
    }

    [Fact]
    public void CreateStepCompleted_WhenResultIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var candidate = new StepCandidate { StepName = "test" };
        var outcome = new ProcessExecutionOutcome
        {
            StepName = "test",
            Status = StepExecutionStatus.Completed,
            Outcome = StepExecutionOutcome.Completed,
            Decision = ExecutionDecisionType.Continue
        };

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateStepCompleted(correlation, context, candidate, outcome, null!));
    }

    [Fact]
    public void CreateStepCompleted_CreatesEventWithOutcome()
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request" };
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = [new StepContext { StepName = "test-step", Version = "1.0" }]
        };
        var candidate = new StepCandidate { StepName = "test-step" };
        var outcome = new ProcessExecutionOutcome
        {
            StepName = "test-step",
            Decision = ExecutionDecisionType.Continue,
            Status = StepExecutionStatus.Completed,
            Outcome = StepExecutionOutcome.Completed
        };
        var result = new ProcessStepInvokerResult();

        var envelope = _factory.CreateStepCompleted(correlation, context, candidate, outcome, result);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Event);
        Assert.Equal("test-step", envelope.Event.StepName);
        Assert.Equal(ExecutionDecisionType.Continue, envelope.Event.DecisionType);
        Assert.Equal(StepExecutionStatus.Completed, envelope.Event.ExecutionStatus);
        Assert.Equal(StepExecutionOutcome.Completed, envelope.Event.Outcome);
    }

    [Fact]
    public void CreateExecutionCompleted_WhenContextIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var executionResult = new ProcessExecutionResult
        {
            ProcessId = Guid.NewGuid(),
            State = ProcessExecutionState.Complete
        };

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateExecutionCompleted(correlation, null!, executionResult));
    }

    [Fact]
    public void CreateExecutionCompleted_WhenExecutionResultIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateExecutionCompleted(correlation, context, null!));
    }

    [Fact]
    public void CreateExecutionCompleted_CreatesEventWithResult()
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request" };
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var executionResult = new ProcessExecutionResult
        {
            ProcessId = Guid.NewGuid(),
            State = ProcessExecutionState.Complete
        };

        var envelope = _factory.CreateExecutionCompleted(correlation, context, executionResult);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Event);
        Assert.Equal(ProcessExecutionState.Complete, envelope.Event.State);
        Assert.Equal(executionResult.ProcessId, envelope.Context.ProcessId);
    }
}
