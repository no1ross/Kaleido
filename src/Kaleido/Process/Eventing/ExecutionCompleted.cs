
namespace Kaleido.Process.Eventing;

[ExcludeFromCodeCoverage]
public sealed record ExecutionCompleted : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public string? RequiredStep { get; init; }

    public string? TargetProcessorName { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    public int? ExecutedStepCount { get; init; }
}
