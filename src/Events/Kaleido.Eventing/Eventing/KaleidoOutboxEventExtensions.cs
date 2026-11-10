using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Kaleido.Eventing;

internal interface IOutboxEventDispatcher
{
    bool CanHandle(string eventType);

    Task PublishAsync(IEventPublisher publisher, KaleidoOutboxEvent entry, CancellationToken cancellationToken);
}

// Turns an event envelope into an outbox entry, and an entry back into the envelope the publisher gets. The event and context types are looked
// up by EventType in the registered Process or Queryable dispatcher - no CLR type name is ever read from the store.
internal static class KaleidoOutboxEventExtensions
{
    private static readonly JsonSerializerOptions Options = new DefaultJsonTypeInfoResolver().ToOutboxJsonOptions();

    public static KaleidoOutboxEvent ToOutboxEvent<TEvent, TContext>(this KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent =>
        envelope.ToOutboxEvent(Options);

    // With the given serializer options (the payload writer's, which redact classified values).
    public static KaleidoOutboxEvent ToOutboxEvent<TEvent, TContext>(this KaleidoEventEnvelope<TEvent, TContext> envelope, JsonSerializerOptions options)
        where TEvent : IKaleidoEvent
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new KaleidoOutboxEvent
        {
            EventId = envelope.Event.EventId,
            EventType = envelope.EventType,
            Payload = JsonSerializer.Serialize(envelope, options),
            OccurredUtc = envelope.Event.OccurredOn,
            NextAttemptUtc = envelope.Event.OccurredOn,
        };
    }

    public static Task PublishOutboxEventAsync(this IEventPublisher publisher, KaleidoOutboxEvent entry, IEnumerable<IOutboxEventDispatcher> dispatchers, CancellationToken cancellationToken) =>
        dispatchers.FirstOrDefault(x => x.CanHandle(entry.EventType)) is { } dispatcher
            ? dispatcher.PublishAsync(publisher, entry, cancellationToken)
            : throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Outbox event '{entry.EventId}' has the unknown event type '{entry.EventType}'.");

    // Deserializes the entry into its envelope type and hands it to the publisher.
    internal static Task PublishOutboxEventAsync<TEvent, TContext>(this IEventPublisher publisher, KaleidoOutboxEvent entry, CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent =>
        publisher.PublishAsync(
            JsonSerializer.Deserialize<KaleidoEventEnvelope<TEvent, TContext>>(entry.Payload, Options)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Outbox event '{entry.EventId}' has an empty payload."),
            cancellationToken);

    // The outbox's JSON form with the given type contracts: web defaults, enums as camelCase
    // strings. The payload writer passes a resolver that redacts classified properties.
    internal static JsonSerializerOptions ToOutboxJsonOptions(this DefaultJsonTypeInfoResolver resolver)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            TypeInfoResolver = resolver,
        };

        options.MakeReadOnly();

        return options;
    }
}
