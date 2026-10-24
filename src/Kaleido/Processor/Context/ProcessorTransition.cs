namespace Kaleido.Processor.Context;

// A process-state transition (ADR 0023): Kaleido decides that this event belongs to this save, and
// runs begin -> save the state -> add the event to the outbox -> commit -> committed. The transaction
// comes from the persistence (IKaleidoTransactions); the state store and the outbox never call each
// other - they are atomic because both write inside this one transaction (e.g. SQLite: one
// transaction on the request's shared DbContext).
internal interface IProcessorTransition
{
    // Saves the state with its event (null: a save without one) and returns the saved context. A
    // concurrency conflict or a failed write rolls back: neither the state nor the event is kept.
    Task<ProcessorContext> SaveAsync(ProcessorContext context, KaleidoOutboxEvent? transitionEvent, CancellationToken cancellationToken);
}

internal sealed class ProcessorTransition(
    IKaleidoTransactions transactions,
    IKaleidoContextStore contextStore,
    IKaleidoOutbox outbox,
    IKaleidoEventWriter eventWriter)
    : IProcessorTransition
{
    public async Task<ProcessorContext> SaveAsync(ProcessorContext context, KaleidoOutboxEvent? transitionEvent, CancellationToken cancellationToken)
    {
        ProcessorContext saved;
        var committed = false;

        try
        {
            await using var transaction = await transactions.BeginAsync(cancellationToken);

            saved = await contextStore.SaveAsync(context, cancellationToken);   // the state, in the transaction

            if (transitionEvent is not null)
            {
                await outbox.AddAsync([transitionEvent], cancellationToken);    // the event, in the same transaction
            }

            await transaction.CommitAsync(cancellationToken);                   // both, or neither
            committed = true;
        }
        finally
        {
            // Not committed (a conflict, a failed write or commit): the event must never be delivered,
            // also when a later save in this scope commits (e.g. the failure save, #451).
            if (!committed && transitionEvent is not null)
            {
                eventWriter.RolledBack();
            }
        }

        if (transitionEvent is not null)
        {
            await eventWriter.CommittedAsync(CancellationToken.None);           // after the commit: the outbox may deliver
        }

        return saved;
    }
}
