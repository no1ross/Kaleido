namespace Kaleido.Eventing;

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
