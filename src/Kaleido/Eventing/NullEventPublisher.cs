namespace Kaleido.Eventing;

/// <summary>
/// Publishes Kaleido domain events to any registered event infrastructure.
/// The default implementation is <see cref="NullEventPublisher"/>, which discards all events.
/// Replace with a real implementation before calling <c>AddKaleido()</c>.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a domain event wrapped in its correlation and traceability context envelope.
    /// </summary>
    Task PublishAsync<TEvent, TContext>(
        KaleidoEventEnvelope<TEvent, TContext> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent;
}

/// <summary>Marker interface for all Kaleido domain events.</summary>
public interface IKaleidoEvent
{
    DateTimeOffset OccurredOn { get; }
}

/// <summary>
/// Default no-op event publisher registered by <c>AddKaleido()</c>.
/// Silently discards all events. Replace with a real implementation before calling
/// <c>AddKaleido()</c> to receive domain events.
/// </summary>
public sealed class NullEventPublisher : IEventPublisher
{
    public Task PublishAsync<TEvent, TContext>(
        KaleidoEventEnvelope<TEvent, TContext> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent
    {
        return Task.CompletedTask;
    }
}
