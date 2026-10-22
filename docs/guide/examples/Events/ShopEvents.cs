using System.Text.Json;
using Kaleido.Eventing;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace Kaleido.Guide.Examples.Events;

// snippet: events-classification
// Your data classifications: mark the fields; the event policy says what each becomes.
public static class ShopClassifications
{
    public static DataClassification Personal { get; } = new("Shop", "Personal");
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class PiiAttribute() : DataClassificationAttribute(ShopClassifications.Personal);
// end snippet

// Where the events go: in a real service, your broker or data warehouse feed.
public interface IWarehouseFeed
{
    Task SendAsync(string eventType, string json, CancellationToken cancellationToken);
}

// snippet: events-publisher
// Kaleido's relay calls the publisher for each stored event, and retries when it throws.
public sealed class WarehousePublisher(IWarehouseFeed feed) : IEventPublisher
{
    public Task PublishAsync<TEvent, TContext>(
        KaleidoEventEnvelope<TEvent, TContext> envelope,
        CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent =>
        feed.SendAsync(envelope.EventType, JsonSerializer.Serialize(envelope), cancellationToken);
}
// end snippet

public static class ShopEvents
{
    public static void AddShopEvents(IKaleidoBuilder kaleido)
    {
        // snippet: events-registration
        kaleido
            .AddEventPublisher<WarehousePublisher>()
            .AddEventRedaction(r =>
                r.SetRedactor<ErasingRedactor>(new DataClassificationSet(ShopClassifications.Personal)));
        // end snippet
    }
}
