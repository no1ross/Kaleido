namespace Kaleido.Process.Participant.Context;

public sealed record StepProcessingRecord
{
    public required object Step { get; init; }

    public required string RequestId { get; init; }

    public StepExecutionStatus Status { get; init; }

    public DateTimeOffset ProcessedOn { get; init; }

    public IReadOnlyCollection<StepProcessingMessage> Messages { get; init; }
        = [];
}