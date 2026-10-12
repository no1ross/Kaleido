using System.Text.Json;
using System.Text.Json.Serialization;
using Kaleido.Processor.Eventing;
using Kaleido.Queryable.Eventing;

namespace Kaleido.Eventing;

// Turns an event envelope into an outbox entry and back. The event and context types are looked
// up by EventType in a fixed map - no CLR type name is ever read from the store.
internal sealed class KaleidoEventSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static KaleidoOutboxEvent ToOutboxEvent<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new KaleidoOutboxEvent
        {
            EventId = envelope.Event.EventId,
            EventType = envelope.EventType,
            Payload = JsonSerializer.Serialize(envelope, Options),
            OccurredUtc = envelope.Event.OccurredOn,
            NextAttemptUtc = envelope.Event.OccurredOn,
        };
    }

    // Deserializes the entry into its envelope type and hands it to the publisher.
    public static Task PublishAsync(IEventPublisher publisher, KaleidoOutboxEvent entry, CancellationToken cancellationToken) =>
        entry.EventType switch
        {
            KaleidoEventTypes.ProcessCreated => PublishAsync<ProcessCreated, ProcessEventContext>(publisher, entry, cancellationToken),
            KaleidoEventTypes.ProcessPlanBuilt => PublishAsync<PlanBuilt, ProcessEventContext>(publisher, entry, cancellationToken),
            KaleidoEventTypes.ProcessStepCompleted => PublishAsync<StepCompleted, ProcessEventContext>(publisher, entry, cancellationToken),
            KaleidoEventTypes.ProcessExecutionCompleted => PublishAsync<ExecutionCompleted, ProcessEventContext>(publisher, entry, cancellationToken),
            KaleidoEventTypes.QueryExecuted => PublishAsync<QueryExecuted, QueryableEventContext>(publisher, entry, cancellationToken),
            _ => throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Outbox event '{entry.EventId}' has the unknown event type '{entry.EventType}'."),
        };

    private static Task PublishAsync<TEvent, TContext>(IEventPublisher publisher, KaleidoOutboxEvent entry, CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent =>
        publisher.PublishAsync(
            JsonSerializer.Deserialize<KaleidoEventEnvelope<TEvent, TContext>>(entry.Payload, Options)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Outbox event '{entry.EventId}' has an empty payload."),
            cancellationToken);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
