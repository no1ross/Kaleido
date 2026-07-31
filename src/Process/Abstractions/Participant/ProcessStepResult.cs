namespace Kaleido.Process.Participant;

public sealed record ProcessStepResult
{
    public ProcessStepOutcome Outcome { get; init; }

    public IReadOnlyCollection<string> RequiredSteps { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepMessage> Messages { get; init; }
        = [];

    public static ProcessStepResult Completed(
        IEnumerable<string>? requiredSteps = null,
        params ProcessStepMessage[] messages)
    {
        return new()
        {
            Outcome = ProcessStepOutcome.Completed,
            RequiredSteps = requiredSteps?.ToArray() ?? [],
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

