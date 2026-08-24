namespace Kaleido.Eventing;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent processEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent;
}

public interface IKaleidoEvent
{
    DateTimeOffset OccurredOn { get; }
}
