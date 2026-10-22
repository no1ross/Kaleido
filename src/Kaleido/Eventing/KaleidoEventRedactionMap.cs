using Microsoft.Extensions.Compliance.Classification;

namespace Kaleido.Eventing;

// The event redaction policy AddEventRedaction configures (ADR 0025): which redactor each set of
// classifications gets in event payloads, and the redactor for the rest. One instance per service
// collection, registered by AddKaleido; separate from the host's log redaction (AddRedaction).
internal sealed class KaleidoEventRedactionMap
{
    private readonly Dictionary<DataClassificationSet, Type> _redactors = [];

    // Whether AddEventRedaction was called: without it, classified values are erased with a warning.
    public bool IsConfigured { get; set; }

    // For classifications without their own redactor; null: they are erased.
    public Type? Fallback { get; set; }

    // A later mapping of the same set replaces an earlier one.
    public void SetRedactor(DataClassificationSet classifications, Type redactor)
    {
        ArgumentNullException.ThrowIfNull(classifications);
        ArgumentNullException.ThrowIfNull(redactor);

        _redactors[classifications] = redactor;
    }

    // The redactor of exactly this set of classifications, else the fallback; null: erase.
    public Type? RedactorFor(DataClassificationSet classifications) =>
        _redactors.TryGetValue(classifications, out var redactor) ? redactor : Fallback;
}
