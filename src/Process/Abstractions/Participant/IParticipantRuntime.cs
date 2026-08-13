
using Kaleido.Exceptions;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;

namespace Kaleido.Process.Participant;

public interface IParticipantRuntime
{
    Task<ParticipantProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessRequest
{
    /// <summary>
    /// Uniquely identifies the durable process instance.
    /// Re-submissions for the same process use the same correlation id.
    /// </summary>
    public Guid? ParticipantProcessId
    {
        get;
        init;
    }

    /// <summary>
    /// Uniquely identifies the current consumer submission.
    /// Each submission should provide a new request id.
    /// </summary>
    public required string RequestId
    {
        get;
        init;
    }

    /// <summary>
    /// Consumer supplied process request data.
    /// </summary>
    public required ParticipantRequest Participant
    {
        get;
        init;
    }
}

public sealed record ParticipantProcessResult
{
    public required Guid ParticipantProcessId { get; init; }

    public required ProcessExecutionState State
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<string> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ParticipantStepResult> Steps
    {
        get;
        init;
    }
        = [];
}

public sealed record ParticipantStepResult
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

    public required object Response
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

    public IReadOnlyCollection<StepProcessingMessage> Messages
    {
        get;
        init;
    }
        = [];
}



public sealed record ParticipantRequest
{
    public IReadOnlyDictionary<string, object?> Steps { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}


