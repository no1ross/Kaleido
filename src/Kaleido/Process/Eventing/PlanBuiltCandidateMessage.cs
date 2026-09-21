namespace Kaleido.Process.Eventing;

public sealed record PlanBuiltCandidateMessage
{
    public required MessageType Type { get; init; }

    public required StepProcessingMessageCode Code { get; init; }

    public required string Message { get; init; }
}
