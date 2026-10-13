using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Logging;

namespace Kaleido.Eventing;

// Turns an event into its outbox entry, protecting sensitive values before anything is written:
// the registered IKaleidoEventRedactors run on the typed envelope, then serialization replaces every
// property that carries a DataClassificationAttribute with its redacted text - also inside the
// object-typed members (a step's request and response, a query's records).
internal interface IKaleidoEventPayloadWriter
{
    KaleidoOutboxEvent Write<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent;
}

internal sealed partial class KaleidoEventPayloadWriter : IKaleidoEventPayloadWriter
{
    private static readonly JsonSerializerOptions FormatOptions = new DefaultJsonTypeInfoResolver().ToOutboxJsonOptions();

    private readonly IKaleidoEventRedactor[] _redactors;
    private readonly IRedactorProvider? _redactorProvider;
    private readonly ILogger<KaleidoEventPayloadWriter> _logger;
    private readonly JsonSerializerOptions _options;
    private int _erasureLogged;

    // The host's IRedactorProvider (AddRedaction) is optional: Kaleido never registers one, so it never
    // shadows the host's. Without one, classified values are erased.
    public KaleidoEventPayloadWriter(
        IEnumerable<IKaleidoEventRedactor> redactors,
        IEnumerable<IRedactorProvider> redactorProviders,
        ILogger<KaleidoEventPayloadWriter> logger)
    {
        _redactors = [.. redactors];
        _redactorProvider = redactorProviders.LastOrDefault();
        _logger = logger;
        _options = new DefaultJsonTypeInfoResolver { Modifiers = { RedactClassifiedProperties } }.ToOutboxJsonOptions();
    }

    public KaleidoOutboxEvent Write<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent
    {
        ArgumentNullException.ThrowIfNull(envelope);

        foreach (var redactor in _redactors)
        {
            envelope = redactor.Redact(envelope)
                ?? throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.TypeMismatch,
                    $"Event redactor '{redactor.GetType().FullName}' returned no envelope for event '{envelope.EventType}'.");
        }

        return envelope.ToOutboxEvent(_options);
    }

    // Runs once per type (the resolver caches the contract): every classified property is replaced by
    // a string property with the same name whose value is the redacted text.
    private void RedactClassifiedProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var i = 0; i < typeInfo.Properties.Count; i++)
        {
            var property = typeInfo.Properties[i];

            if (property.Get is not { } get || Classifications(property) is not { } classifications)
            {
                continue;
            }

            var redact = RedactorFor(classifications);
            var redacted = typeInfo.CreateJsonPropertyInfo(typeof(string), property.Name);
            redacted.Get = owner => get(owner) is { } value ? redact(Format(value)) : null;
            redacted.Order = property.Order;
            typeInfo.Properties[i] = redacted;
        }
    }

    // The property's classifications (union of its attributes), or null when it is not classified.
    private static DataClassificationSet? Classifications(JsonPropertyInfo property)
    {
        var classifications = (property.AttributeProvider?.GetCustomAttributes(typeof(DataClassificationAttribute), inherit: true) ?? [])
            .OfType<DataClassificationAttribute>()
            .Select(x => x.Classification)
            .Where(x => x != DataClassification.None)
            .ToArray();

        return classifications.Length == 0 ? null : new DataClassificationSet(classifications);
    }

    private Func<string, string> RedactorFor(DataClassificationSet classifications)
    {
        if (_redactorProvider?.GetRedactor(classifications) is { } redactor)
        {
            return redactor.Redact;
        }

        return _ =>
        {
            if (Interlocked.Exchange(ref _erasureLogged, 1) == 0)
            {
                LogClassifiedValuesErased(_logger);
            }

            return string.Empty;
        };
    }

    // Text to redact: strings as they are, numbers and dates invariant, anything else as JSON.
    private static string Format(object value) =>
        value switch
        {
            string text => text,
            IFormattable formattable => formattable.ToString(format: null, CultureInfo.InvariantCulture),
            _ => JsonSerializer.Serialize(value, value.GetType(), FormatOptions),
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event payloads contain classified values (DataClassificationAttribute) and no IRedactorProvider is registered: the values are erased. Register AddRedaction to choose how each classification is redacted (erase, HMAC, encryption).")]
    private static partial void LogClassifiedValuesErased(ILogger logger);
}
