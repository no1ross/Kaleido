namespace Kaleido.Processor.Execution;

[ExcludeFromCodeCoverage]
internal sealed record ProcessExecutionOutcome
{
    public required string StepName { get; init; }
    public required StepExecutionStatus Status { get; init; }
    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages { get; init; } = [];
    public IReadOnlyCollection<ProcessMessage> BusinessMessages { get; init; } = [];
    public object? Response { get; init; }
}
