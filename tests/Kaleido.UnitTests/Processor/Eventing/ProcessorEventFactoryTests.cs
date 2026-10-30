using Kaleido.Observability;
using Kaleido.Processor.Context;
using Kaleido.Processor.Registry;

using Kaleido.UnitTests;

namespace Kaleido.Processor.Eventing.UnitTests;

public sealed class ProcessorEventFactoryTests
    : SutFixture
{
    private readonly KaleidoServiceOptions _options = new() { ServiceName = "test-service" };

    private ProcessorEventFactory Sut => CreateSut();

    private ProcessorEventFactory CreateSut() =>
        new(_options);

    [Fact]
    public void CreateProcessCreated_WhenContextIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreateProcessCreated(correlation, null!, request));
    }

    [Fact]
    public void CreateProcessCreated_WhenRequestIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreateProcessCreated(correlation, context, null!));
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

        var result = Sut.CreateProcessCreated(correlation, context, request);

        Assert.NotNull(result);
        Assert.NotNull(result.Context);
        Assert.Equal("test-request", result.Context.RequestId);
        Assert.Equal("test-service", result.Context.ServiceName);
        Assert.Equal(context.ProcessId, result.Context.ProcessId);
        Assert.NotNull(result.Event);
        Assert.Equal(ProcessExecutionState.Active, result.Event.State);
    }

    [Theory]
    [InlineData("alice", "sub-alice")]
    [InlineData(null, null)]
    public void CreateProcessCreated_TagsTheCaller_OrNoneWhenAnonymous(string? name, string? id)
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request", CallerName = name, CallerId = id };
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };

        var result = Sut.CreateProcessCreated(correlation, context, request);

        Assert.Equal(name is null ? null : new KaleidoCaller { Name = name, Id = id }, result.Context.Caller);
    }

    // ADR 0022: the front door (informational) and the acting application (from the principal) reach the events.
    [Fact]
    public void CreateProcessCreated_RecordsTheOriginAndTheCallersActor()
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request", Origin = "mcp", OriginTool = "clinic_start_visit", CallerActor = "intake" };
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };

        var result = Sut.CreateProcessCreated(correlation, context, request);

        Assert.Equal(("mcp", "clinic_start_visit", "intake"), (result.Context.Origin, result.Context.OriginTool, result.Context.CallerActor));
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

        var result = Sut.CreateProcessCreated(correlation, context, request);

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
            Sut.CreatePlanBuilt(correlation, null!, request, plan, 0));
    }

    [Fact]
    public void CreatePlanBuilt_WhenRequestIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var plan = new ExecutionPlanResult { Candidates = [] };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreatePlanBuilt(correlation, context, null!, plan, 0));
    }

    [Fact]
    public void CreatePlanBuilt_WhenPlanIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?>() } };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreatePlanBuilt(correlation, context, request, null!, 0));
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
                new ProcessStepMetadata { Name = "test-step", Description = "desc", DisplayName = "display" }),
            IncludedInExecutionPlan = true
        };
        candidate.AddError(ProcessorErrorCodes.Required, "Name is required.");

        var plan = new ExecutionPlanResult { Candidates = [candidate] };

        var result = Sut.CreatePlanBuilt(correlation, context, request, plan, 1);

        Assert.NotNull(result);
        Assert.NotNull(result.Event);
        Assert.Single(result.Event.Candidates);
        Assert.Equal("test-step", result.Event.Candidates.First().StepName);
        Assert.Equal(StepCandidateStatus.Built, result.Event.Candidates.First().CandidateStatus);
        Assert.True(result.Event.Candidates.First().IncludedInExecutionPlan);
        Assert.Equal(
            ProcessorErrorCodes.Required,
            Assert.Single(result.Event.Candidates.First().Messages).Code);
    }

    [Fact]
    public void CreatePlanBuilt_ACandidateWithoutARegistration_IsRecordedInvalid()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var request = new ProcessRequest { Processor = new ProcessorRequest { Steps = new Dictionary<string, object?> { ["unknown"] = null } } };
        var plan = new ExecutionPlanResult { Candidates = [StepCandidate.Invalid("unknown", ProcessorErrorCodes.InvalidRequest, "unknown step")] };

        var result = Sut.CreatePlanBuilt(new KaleidoCorrelationContext(), context, request, plan, 0);

        var candidate = Assert.Single(result.Event.Candidates);
        Assert.Equal(StepCandidateStatus.Invalid, candidate.CandidateStatus);
    }

    private static ProcessExecutionOutcome CompletedOutcome() =>
        new()
        {
            StepName = "test-step",
            Status = StepExecutionStatus.Completed
        };

    [Fact]
    public void CreateStepCompleted_TakesTheHistoryFromTheStepInTheProcess()
    {
        var executed = DateTimeOffset.UtcNow;
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = [new StepContext { StepName = "test-step", LatestRequestId = "req-7", LastExecuted = executed }]
        };

        var envelope = Sut.CreateStepCompleted(new KaleidoCorrelationContext(), context, new StepCandidate { StepName = "test-step" }, CompletedOutcome(), ExecutionDecisionType.Continue, new StepInvocationResult());

        Assert.Equal("req-7", envelope.Event.StepLatestRequestId);
        Assert.Equal(executed, envelope.Event.StepLastExecuted);
    }

    [Fact]
    public void CreateStepCompleted_ForAStepNotInTheProcess_HasNoHistory()
    {
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var envelope = Sut.CreateStepCompleted(new KaleidoCorrelationContext(), context, new StepCandidate { StepName = "test-step" }, CompletedOutcome(), ExecutionDecisionType.Continue, new StepInvocationResult());

        Assert.Null(envelope.Event.StepLatestRequestId);
        Assert.Null(envelope.Event.StepLastExecuted);
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
        };
        var result = new StepInvocationResult();

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreateStepCompleted(correlation, null!, candidate, outcome, ExecutionDecisionType.Continue, result));
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
        };
        var result = new StepInvocationResult();

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreateStepCompleted(correlation, context, null!, outcome, ExecutionDecisionType.Continue, result));
    }

    [Fact]
    public void CreateStepCompleted_WhenOutcomeIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };
        var candidate = new StepCandidate { StepName = "test" };
        var result = new StepInvocationResult();

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreateStepCompleted(correlation, context, candidate, null!, ExecutionDecisionType.Continue, result));
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
        };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreateStepCompleted(correlation, context, candidate, outcome, ExecutionDecisionType.Continue, null!));
    }

    [Fact]
    public void CreateStepCompleted_CreatesEventWithOutcome()
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request" };
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Steps = [new StepContext { StepName = "test-step" }]
        };
        var candidate = new StepCandidate { StepName = "test-step" };
        var outcome = new ProcessExecutionOutcome
        {
            StepName = "test-step",
            Status = StepExecutionStatus.Completed
        };
        var result = new StepInvocationResult();

        var envelope = Sut.CreateStepCompleted(correlation, context, candidate, outcome, ExecutionDecisionType.Continue, result);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Event);
        Assert.Equal("test-step", envelope.Event.StepName);
        Assert.Equal(ExecutionDecisionType.Continue, envelope.Event.DecisionType);
        Assert.Equal(StepExecutionStatus.Completed, envelope.Event.Status);
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
            Sut.CreateExecutionCompleted(correlation, null!, executionResult));
    }

    [Fact]
    public void CreateExecutionCompleted_WhenExecutionResultIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "test" };

        Assert.Throws<ArgumentNullException>(() =>
            Sut.CreateExecutionCompleted(correlation, context, null!));
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

        var envelope = Sut.CreateExecutionCompleted(correlation, context, executionResult);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Event);
        Assert.Equal(ProcessExecutionState.Complete, envelope.Event.State);
        Assert.Equal(executionResult.ProcessId, envelope.Context.ProcessId);
    }
}
