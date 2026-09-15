namespace Kaleido.Eventing;

/// <summary>
/// Default no-op event publisher registered by <c>AddKaleido()</c>.
/// Silently discards all events. Replace with a real implementation before calling
/// <c>AddKaleido()</c> to receive domain events.
/// </summary>
public sealed class NullEventPublisher : IEventPublisher
{
    public Task PublishAsync<TEvent>(
        TEvent processEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent
    {
        return Task.CompletedTask;
    }
}
