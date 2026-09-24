namespace Kaleido.Eventing;

/// <summary>
/// Wraps a Kaleido domain event with its correlation and traceability context.
/// The event carries pure business data; the context carries routing and observability metadata.
/// </summary>
/// <typeparam name="TEvent">The domain event type.</typeparam>
/// <typeparam name="TContext">The context type — either <see cref="ProcessEventContext"/> or <see cref="QueryableEventContext"/>.</typeparam>
[ExcludeFromCodeCoverage]
public sealed record KaleidoEventEnvelope<TEvent, TContext>
    where TEvent : IKaleidoEvent
{
    /// <summary>The correlation and traceability context for this event.</summary>
    public required TContext Context { get; init; }

    /// <summary>The domain event payload.</summary>
    public required TEvent Event { get; init; }
}
