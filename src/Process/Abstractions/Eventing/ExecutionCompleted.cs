using Kaleido.Eventing;
using Kaleido.Process.Execution;

namespace Kaleido.Process.Eventing;

[KaleidoEvent(Type = "process.execution-completed.v1")]
public sealed record ExecutionCompleted : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public ProcessStepReference? RequiredStep { get; init; }

    public IReadOnlyCollection<ProcessStepReference> AvailableSteps { get; init; } = [];

    public int? ExecutedStepCount { get; init; }
}
