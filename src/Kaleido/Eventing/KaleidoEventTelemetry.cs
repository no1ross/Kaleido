namespace Kaleido.Eventing;

/// <summary>
/// Meter and instrument names of the event outbox and relay.
/// </summary>
public static class KaleidoEventTelemetry
{
    /// <summary>The meter of the event outbox and relay.</summary>
    public const string MeterName = "Kaleido.Events";

    /// <summary>Events handed to the publisher successfully.</summary>
    public const string PublishedCounterName = "kaleido.events.published";

    /// <summary>Failed publish attempts (each is retried later, or parked).</summary>
    public const string FailedCounterName = "kaleido.events.failed";

    /// <summary>Events parked after the last retry (kept, never retried).</summary>
    public const string ParkedCounterName = "kaleido.events.parked";

    /// <summary>Events that could not be written to the outbox (not part of a state save).</summary>
    public const string EnqueueFailuresCounterName = "kaleido.events.enqueue_failures";

    /// <summary>Tag: the event type (<see cref="KaleidoEventTypes"/>).</summary>
    public const string TagEventType = "kaleido.event.type";
}
