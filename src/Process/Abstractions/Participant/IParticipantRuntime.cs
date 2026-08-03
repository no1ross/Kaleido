
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
    public required string CorrelationId
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

//public class ProcessResult
//{
//    public string? CorrelationId { get; init; }
//    public StepExecutionOutcome Outcome { get; init; }
//    public IReadOnlyCollection<StepProcessingMessage> Messages { get; init; } = [];
//    public IReadOnlyCollection<ProcessStepDefinition> RequiredSteps { get; init; } = [];
//}

//public class ParticipantDefinition
//{
//    public string Name { get; init; } = string.Empty;
//    public string Description { get; init; } = string.Empty;
//    public IReadOnlyCollection<ProcessStepDefinition> Steps { get; init; } = [];
//}

//public class ProcessStepDefinition
//{
//    public string Name { get; set; } = string.Empty;
//    public string Description { get; init; } = string.Empty;
//    public IReadOnlyCollection<FieldDefinition> Fields { get; set; } = [];
//}

//public class FieldDefinition
//{
//    public string Name { get; set; } = string.Empty;
//    public Type Type { get; set; } = typeof(string);
//    public bool IsRequired { get; set; } = false;
//}

public sealed record ParticipantRequest
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Steps { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
}


