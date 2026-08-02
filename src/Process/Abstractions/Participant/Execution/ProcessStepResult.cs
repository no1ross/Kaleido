using Kaleido.Process.Participant;

public sealed record ProcessStepResult
{
    public bool Succeeded { get; init; }

    public string? RequiredStep { get; init; }

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];

    public static ProcessStepResult Success(
        string? requiredStep = null,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            RequiredStep = requiredStep,
            Messages = messages
        };
    }

    public static ProcessStepResult Failure(
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = false,
            Messages = messages
        };
    }
}