namespace Kaleido.Eventing;

// Kaleido's built-in outbox integration (the default): events are added to the outbox store - inside
// the transition's transaction when the store's persistence opened one - and the relay, woken once
// they are committed, hands them to the publisher.
internal sealed class KaleidoStoreOutbox(IKaleidoOutboxStore store, IKaleidoEventSignal signal) : IKaleidoPublisherOutbox
{
    public Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken) =>
        store.AddAsync(events, cancellationToken);

    public Task CommittedAsync(CancellationToken cancellationToken)
    {
        signal.Notify();
        return Task.CompletedTask;
    }

    // The events were written inside the transaction, so its rollback already removed them; one the
    // store wrote outside a transaction (no persistence transaction) stays, as the store documents.
    public void Discard()
    {
    }
}
