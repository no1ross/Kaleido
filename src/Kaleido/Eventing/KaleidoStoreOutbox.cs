namespace Kaleido.Eventing;

// Kaleido's outbox integration: events are added to the outbox store - inside the transition's
// transaction when the store's persistence opened one, so a rollback removes them - and this host's
// relay, if it runs one, is woken once they are committed. A relay elsewhere finds them by polling.
internal sealed class KaleidoStoreOutbox(IKaleidoOutboxStore store, IKaleidoEventSignal signal) : IKaleidoOutbox
{
    public Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken) =>
        store.AddAsync(events, cancellationToken);

    public Task CommittedAsync(CancellationToken cancellationToken)
    {
        signal.Notify();
        return Task.CompletedTask;
    }
}
