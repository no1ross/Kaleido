using Kaleido.Process.Participant.Planning;

namespace Kaleido.Process.Participant;

public sealed record StepProcessingMessage
{
    public required MessageType Type { get; init; }

    public required StepProcessingMessageCode Code { get; init; }

    public required string Message { get; init; }

    public static StepProcessingMessage Information(
        StepProcessingMessageCode code,
        string message)
    {
        return new()
        {
            Type = MessageType.Information,
            Code = code,
            Message = message
        };
    }

    public static StepProcessingMessage Warning(
        StepProcessingMessageCode code,
        string message)
    {
        return new()
        {
            Type = MessageType.Warning,
            Code = code,
            Message = message
        };
    }

    public static StepProcessingMessage Error(
        StepProcessingMessageCode code,
        string message)
    {
        return new()
        {
            Type = MessageType.Error,
            Code = code,
            Message = message
        };
    }
}
