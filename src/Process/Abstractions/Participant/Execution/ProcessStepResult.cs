
namespace Kaleido.Process.Participant.Execution;

public interface IProcessStepHandlerResult
{
    bool Succeeded { get; }

    string? RequiredStep { get; }

    object? Response { get; }

    IReadOnlyCollection<ProcessMessage> Messages { get; }
}

public sealed record ProcessStepHandlerResult<TProcessStepResult> : IProcessStepHandlerResult
{
    public bool Succeeded { get; init; }

    public string? RequiredStep { get; init; }

    public required TProcessStepResult Response { get; init; }

    object IProcessStepHandlerResult.Response => Response!;

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];

    public static ProcessStepHandlerResult<TProcessStepResult> Success(
        TProcessStepResult response,
        string? requiredStep = null,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = true,
            RequiredStep = requiredStep,
            Messages = messages,
            Response = response
        };
    }

    public static ProcessStepHandlerResult<TProcessStepResult> Failure(
        TProcessStepResult response,
        params ProcessMessage[] messages)
    {
        return new()
        {
            Succeeded = false,
            Messages = messages,
            Response = response
        };
    }
}

public record ProcessStepHandlerResult
    : IProcessStepHandlerResult
{
    public bool Succeeded { get; init; }

    public string? RequiredStep { get; init; }

    public object? Response { get; init; }

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

    public static ProcessStepHandlerResult Success(
        params ProcessMessage[] messages)
    {
        return Success(null, messages);
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
