namespace Kaleido.Process.Participant.Context;

public sealed record ParticipantContext
{
    public string? CorrelationId { get; init; }

    public IReadOnlyCollection<StepContext> ProcessSteps { get; init; }
        = [];

    public ParticipantContext AddStepContext(StepContext step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return this with
        {
            ProcessSteps =
            [
                .. ProcessSteps,
                step
            ]
        };
    }

    public StepContext? FindStep(string stepName)
    {
        return ProcessSteps.FirstOrDefault(
            x => string.Equals(
                x.StepName,
                stepName,
                StringComparison.OrdinalIgnoreCase));
    }

    public bool HasCompletedStep(string stepName)
    {
        return ProcessSteps.Any(
            x => string.Equals(
                x.StepName,
                stepName,
                StringComparison.OrdinalIgnoreCase)
            && x.Outcome == StepExecutionOutcome.Completed);
    }
}
