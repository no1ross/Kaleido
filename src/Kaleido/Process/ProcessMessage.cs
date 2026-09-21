namespace Kaleido.Process;

public sealed record ProcessMessage
{
    public required string Code { get; init; }

    public required MessageType Type { get; init; }

    public required string Message { get; init; }
}