using Kaleido.Exceptions;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;
using Kaleido.Process.Context;

namespace Kaleido.Process;

public interface IProcessorRuntime
{
    Task<ProcessorProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessRequest
{
    /// <summary>
    /// Uniquely identifies the durable process instance.
    /// Re-submissions for the same process use the same correlation id.
    /// Null creates a new process instance.
    /// </summary>
    public Guid? ProcessId
    {
        get;
        init;
    }

    /// <summary>
    /// Consumer supplied process request data.
    /// </summary>
    public required ProcessorRequest Processor
    {
        get;
        init;
    }
}

public sealed record ProcessorProcessResult
{
    public required Guid ProcessId { get; init; }

    public required ProcessExecutionState State
    {
        get;
        init;
    }

    public ProcessStepReference? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepReference> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessorStepResult> Steps
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessorStepResult
{
    public required string StepName
    {
        get;
        init;
    }

    public required StepCandidateStatus CandidateStatus
    {
        get;
        init;
    }

    public required bool IncludedInExecutionPlan
    {
        get;
        init;
    }

    public object? Response
    {
        get;
        init;
    }

    public StepExecutionStatus? ExecutionStatus
    {
        get;
        init;
    }

    public ExecutionDecisionType? Decision
    {
        get;
        init;
    }

    public StepExecutionOutcome? Outcome
    {
        get;
        init;
    }

    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessMessage> BusinessMessages
    {
        get;
        init;
    }
= [];
}



public sealed record ProcessorRequest
{
    public IReadOnlyDictionary<string, object?> Steps { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}


