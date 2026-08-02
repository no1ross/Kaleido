namespace Kaleido.Process.Participant.Context;

public sealed record StepContext
{
    public string StepName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public IReadOnlyCollection<StepProcessingRecord> History { get; init; }
        = [];

    public StepExecutionOutcome Outcome { get; init; }

    public DateTimeOffset LastProcessed { get; init; }

    public StepContext AddStepRequestContext(StepProcessingRecord step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return this with
        {
            History =
            [
                .. History,
                step
            ]
        };
    }
}
