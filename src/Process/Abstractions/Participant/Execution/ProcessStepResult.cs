
namespace Kaleido.Process.Participant.Execution;

public sealed record ProcessStepHandlerResult
{
    public bool Succeeded { get; init; }

    public string? RequiredStep { get; init; }

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];

    public static ProcessStepHandlerResult Success(
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

    public static ProcessStepHandlerResult Failure(
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = false,
            Messages = messages
        };
    }
}