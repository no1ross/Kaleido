namespace Kaleido.Storage;

// How Kaleido makes its events durable (ADR 0023, #552): every service writes every event to its
// outbox store - a process-state transition's event inside the transition's transaction - and the relay
// (AddOutboxRelay, in this host or another on the same database) publishes them. One implementation,
// KaleidoStoreOutbox; internal since the host's own outbox and direct publication were removed (#552).
internal interface IKaleidoOutbox
{
    // Adds events (never empty): within the current transition's transaction when Kaleido began one,
    // otherwise on their own. A failure within a transition fails it; outside one it is logged.
    Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken);

    // After the events added since the last call are committed: wakes this host's relay, if it runs one.
    Task CommittedAsync(CancellationToken cancellationToken);
}
