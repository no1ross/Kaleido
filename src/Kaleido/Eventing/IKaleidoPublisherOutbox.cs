namespace Kaleido.Eventing;

// An outbox integration that delivers through IEventPublisher (the built-in outbox, direct
// publication): without a registered publisher Kaleido produces no events for it. The host's own
// outbox delivers itself and needs no publisher.
internal interface IKaleidoPublisherOutbox : IKaleidoOutbox
{
    // The transition whose events were added since the last CommittedAsync did not commit: forget
    // them (#451). An outbox that writes inside the transaction has nothing to do - the rollback
    // already removed them; one that holds them in memory (direct publication) must never deliver them.
    void Discard();
}
