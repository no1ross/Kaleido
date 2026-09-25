namespace Kaleido.Process.Eventing;

public interface IProcessEvent : IKaleidoEvent
{
}

[ExcludeFromCodeCoverage]
public abstract record ProcessEventBase : IProcessEvent
{
    public required DateTimeOffset OccurredOn { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record ProcessCreated : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; init; }

    public IReadOnlyCollection<string> SubmittedStepNames { get; init; } = [];

    public required int SubmittedStepCount { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record PlanBuiltCandidateMessage
{
    public required MessageType Type { get; init; }

    public required StepProcessingMessageCode Code { get; init; }

    public required string Message { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record PlanBuiltCandidate
{
    public required string StepName { get; init; }

    public required string StepVersion { get; init; }

    public required StepCandidateStatus CandidateStatus { get; init; }

    public required bool IncludedInExecutionPlan { get; init; }

    public IReadOnlyCollection<PlanBuiltCandidateMessage> Messages { get; init; } = [];
}

[ExcludeFromCodeCoverage]
public sealed record PlanBuilt : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public string? RequiredStep { get; init; }

    public string? TargetProcessorName { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    public IReadOnlyCollection<string> SubmittedStepNames { get; init; } = [];

    public required int SubmittedStepCount { get; init; }

    public required int CandidateCount { get; init; }

    public required int ExecutableCount { get; init; }

    public IReadOnlyCollection<PlanBuiltCandidate> Candidates { get; init; } = [];
}

[ExcludeFromCodeCoverage]
public sealed record StepCompleted : ProcessEventBase
{
    public required string StepName { get; init; }

    public required string StepVersion { get; init; }

    public object? Request { get; init; }

    public object? Response { get; init; }

    public required ExecutionDecisionType DecisionType { get; init; }

    public required StepExecutionStatus ExecutionStatus { get; init; }

    public required StepExecutionOutcome Outcome { get; init; }

    public IReadOnlyCollection<ProcessMessage> BusinessMessages { get; init; } = [];

    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages { get; init; } = [];

    public required ProcessExecutionState ProcessState { get; init; }

    public string? RequiredStep { get; init; }

    public string? TargetProcessorName { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    public string? StepLatestRequestId { get; init; }

    public DateTimeOffset? StepLastExecuted { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record ExecutionCompleted : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public string? RequiredStep { get; init; }

    public string? TargetProcessorName { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    public int? ExecutedStepCount { get; init; }
}
