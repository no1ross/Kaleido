using Kaleido.Processor.Context;

namespace Kaleido.Processor.Recovery;

// A step outcome Kaleido could not save (#537): the handler ran, so the context it would have saved
// (with the version it was based on) and the step's event are kept until the recovery service applies
// them. In memory only: lost on restart (the Critical log keeps it) and seen by this instance only.
internal sealed record PendingOutcome
{
    public required ProcessorContext Context { get; init; }

    public required string StepName { get; init; }

    public KaleidoOutboxEvent? TransitionEvent { get; init; }

    public DateTimeOffset RecordedUtc { get; init; }

    public DateTimeOffset NextAttemptUtc { get; init; }

    public int Attempts { get; init; }
}
