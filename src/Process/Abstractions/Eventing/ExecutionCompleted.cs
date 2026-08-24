using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.Eventing;

public sealed record ExecutionCompleted : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public string? RequiredStep { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    public int? ExecutedStepCount { get; init; }
}
