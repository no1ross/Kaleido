namespace Kaleido.Process.Participant;

public sealed class ExecutionCandidate
{
    private readonly List<ProcessStepMessage> _messages = [];

    public string StepName { get; init; } = string.Empty;

    public ProcessStepRegistration? Registration { get; init; }

    public object? Step { get; set; }

    public ExecutionCandidateStatus Status { get; set; } =
        ExecutionCandidateStatus.Pending;

    public IReadOnlyCollection<ProcessStepMessage> Messages =>
        _messages;

    public bool HasErrors =>
        _messages.Any(x => x.Type == ProcessStepMessageType.Error);

    public TStep GetStep<TStep>()
        where TStep : class
    {
        return (TStep)Step!;
    }

    public void AddMessage(
        ProcessStepMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _messages.Add(message);
    }

    public static ExecutionCandidate Invalid(
        string stepName,
        ProcessStepMessage message)
    {
        var candidate =
            new ExecutionCandidate
            {
                StepName = stepName,
                Status = ExecutionCandidateStatus.Invalid
            };

        candidate.AddMessage(message);

        return candidate;
    }
}

internal sealed record ExecutionCandidateBuilderResult
{
    public IReadOnlyCollection<ExecutionCandidate> Candidates
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessStepMessage
{
    public ProcessStepMessageType Type { get; init; }

    public ProcessStepMessageCode Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public static ProcessStepMessage Information(
        ProcessStepMessageCode code,
        string message)
    {
        return new()
        {
            Type = ProcessStepMessageType.Information,
            Code = code,
            Message = message
        };
    }

    public static ProcessStepMessage Warning(
        ProcessStepMessageCode code,
        string message)
    {
        return new()
        {
            Type = ProcessStepMessageType.Warning,
            Code = code,
            Message = message
        };
    }

    public static ProcessStepMessage Error(
        ProcessStepMessageCode code,
        string message)
    {
        return new()
        {
            Type = ProcessStepMessageType.Error,
            Code = code,
            Message = message
        };
    }
}

//public class ExecutionPlan
//{
//    public IReadOnlyCollection<ExecutionStep> Steps { get; set; } = [];
//    public IReadOnlyCollection<ValidationError> Errors { get; set; } = [];
//}

//public class ExecutionStep
//{
//    public ProcessStepRegistration? Registration { get; set; }
//    public ProcessStepRequest? Request { get; set; }
//    public int Order { get; set; } = 0;
//}
