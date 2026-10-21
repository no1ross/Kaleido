namespace Kaleido.Eventing;

// An outbox integration that delivers through IEventPublisher (the built-in outbox, direct
// publication): without a registered publisher Kaleido produces no events for it. The host's own
// outbox delivers itself and needs no publisher.
internal interface IKaleidoPublisherOutbox : IKaleidoOutbox;
