using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.Eventing;

public sealed record ProcessCreated : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; init; }

    public IReadOnlyCollection<string> SubmittedStepNames { get; init; } = [];

    public required int SubmittedStepCount { get; init; }
}
