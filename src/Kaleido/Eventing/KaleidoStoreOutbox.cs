namespace Kaleido.Eventing;

// Kaleido's built-in outbox integration (the default): events are added to the outbox store - inside
// the transition's transaction when the store's persistence opened one - and the relay, woken once
// they are committed, hands them to the publisher.
internal sealed class KaleidoStoreOutbox(IKaleidoOutboxStore store, IKaleidoEventSignal signal) : IKaleidoPublisherOutbox
{
    public Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default) =>
        store.AddAsync(events, cancellationToken);

    public Task CommittedAsync(CancellationToken cancellationToken = default)
    {
        signal.Notify();
        return Task.CompletedTask;
    }
}
