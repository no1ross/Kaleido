using Kaleido.Process.Context;

namespace Kaleido.Process.Recovery;

// Decides what becomes of a step outcome whose save failed after the handler ran (#537): kept for the
// recovery service when it can be applied later, otherwise left to an operator. The executor reports
// the decision through its step observation.
internal interface IProcessorOutcomeRecovery
{
    UnsavedOutcome Keep(ProcessContext attempted, KaleidoOutboxEvent? transitionEvent, string stepName, Exception failure);
}

internal sealed class ProcessorOutcomeRecovery(
    IProcessorRecoveryStore store,
    TimeProvider time)
    : IProcessorOutcomeRecovery
{
    public UnsavedOutcome Keep(ProcessContext attempted, KaleidoOutboxEvent? transitionEvent, string stepName, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(attempted);
        ArgumentNullException.ThrowIfNull(failure);

        // The process moved on since the handler saw it: this outcome can never be applied.
        if (failure is ProcessConcurrencyException)
        {
            return UnsavedOutcome.Conflict;
        }

        var now = time.GetUtcNow();
        var kept = store.TryRecord(new PendingOutcome
        {
            Context = attempted,
            StepName = stepName,
            TransitionEvent = transitionEvent,
            RecordedUtc = now,
            NextAttemptUtc = now,
        });

        return kept ? UnsavedOutcome.PendingRecovery : UnsavedOutcome.NotSaved;
    }
}
