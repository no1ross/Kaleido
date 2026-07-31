namespace Kaleido.Process.Participant;

public sealed record ProcessStepResult
{
    public ProcessStepOutcome Outcome { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepMessage>? Messages { get; init; }
        = [];

    public static ProcessStepResult Completed(
        params ProcessStepMessage[] messages)
    {
        return new()
        {
            Outcome = ProcessStepOutcome.Completed,
            Messages = messages
        };
    }

    public static ProcessStepResult Blocked(
        IEnumerable<string> availableSteps,
        params ProcessStepMessage[] messages)
    {
        return new()
        {
            Outcome = ProcessStepOutcome.Blocked,
            AvailableSteps = availableSteps.ToArray(),
            Messages = messages
        };
    }

    public static ProcessStepResult Failed(
        params ProcessStepMessage[] messages)
    {
        return new()
        {
            Outcome = ProcessStepOutcome.Failed,
            Messages = messages
        };
    }
}


