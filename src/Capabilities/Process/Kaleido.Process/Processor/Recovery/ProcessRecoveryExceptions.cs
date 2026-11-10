namespace Kaleido.Processor.Recovery;

// What became of a step outcome Kaleido could not save (#537).
internal enum UnsavedOutcome
{
    // Kept: the recovery service applies it when the store is back (503).
    PendingRecovery,

    // Neither saved nor kept (the recovery store is full): an operator reconciles it (500).
    NotSaved,

    // Another request changed the process meanwhile: it can never be applied (409).
    Conflict,
}

// The step's handler ran, but its outcome was not saved. The transport answers so that the caller
// knows the step ran and does not send it again (#537).
[ExcludeFromCodeCoverage]
internal sealed class StepOutcomeNotSavedException(Guid processId, string stepName, UnsavedOutcome outcome, Exception innerException)
    : Exception(Describe(processId, stepName, outcome), innerException)
{
    public Guid ProcessId { get; } = processId;

    public string StepName { get; } = stepName;

    public UnsavedOutcome Outcome { get; } = outcome;

    public string Code =>
        Outcome == UnsavedOutcome.PendingRecovery
            ? ProcessorErrorCodes.OutcomePendingRecovery
            : ProcessorErrorCodes.OutcomeNotSaved;

    private static string Describe(Guid processId, string stepName, UnsavedOutcome outcome) =>
        outcome switch
        {
            UnsavedOutcome.PendingRecovery =>
                $"Step '{stepName}' ran, but process '{processId}' could not be saved. Its outcome is kept and is applied when the store is available again. Do not send the step again; read the process state later.",
            UnsavedOutcome.Conflict =>
                $"Step '{stepName}' ran, but process '{processId}' was changed by another request meanwhile, so its outcome was not saved. Do not send the step again: its own work may have happened, and the process must be reconciled.",
            _ =>
                $"Step '{stepName}' ran, but process '{processId}' could not be saved, and its outcome could not be kept for recovery. Do not send the step again: its own work may have happened, and the process must be reconciled.",
        };
}

// A process whose earlier step outcome is still being saved: nothing runs on it until then (#537).
[ExcludeFromCodeCoverage]
internal sealed class ProcessRecoveringException(Guid processId)
    : Exception($"Process '{processId}' is being recovered: the outcome of an earlier step is still being saved. Nothing was run; try again shortly.")
{
    public Guid ProcessId { get; } = processId;
}
