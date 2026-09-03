using Kaleido.Eventing;
using Kaleido.Process.Execution;

namespace Kaleido.Process.Eventing;

[KaleidoEvent(Type = "process.execution-completed.v1")]
public sealed record ExecutionCompleted : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public string? RequiredStep { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    public int? ExecutedStepCount { get; init; }
}
