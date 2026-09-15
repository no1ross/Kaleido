namespace Kaleido.Eventing;

/// <summary>
/// Publishes Kaleido domain events to any registered event infrastructure.
/// The default implementation is <see cref="NullEventPublisher"/>, which discards all events.
/// Replace with a real implementation before calling <c>AddKaleido()</c>.
/// </summary>
public interface IEventPublisher
{
    /// <summary>Publishes a single typed domain event asynchronously.</summary>
    Task PublishAsync<TEvent>(
        TEvent processEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent;
}

/// <summary>Marker interface for all Kaleido domain events.</summary>
public interface IKaleidoEvent
{
    DateTimeOffset OccurredOn { get; }
}
