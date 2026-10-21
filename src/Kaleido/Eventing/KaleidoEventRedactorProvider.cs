using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Eventing;

// The redactors of event payloads, per the policy AddEventClassificationRedaction configured (ADR 0025).
// Deliberately not an IRedactorProvider registration: the host's IRedactorProvider (AddRedaction)
// stays the log policy, untouched, and logs and events are configured independently.
internal interface IKaleidoEventRedactorProvider
{
    // Whether AddEventClassificationRedaction was called (without it, classified values are erased with a warning).
    bool IsConfigured { get; }

    // The redactor for a property's classifications: its own, else the fallback; null when the value
    // is to be erased.
    Redactor? GetRedactor(DataClassificationSet classifications);
}

internal sealed class KaleidoEventRedactorProvider(KaleidoEventRedactionMap map, IServiceProvider services)
    : IKaleidoEventRedactorProvider
{
    public bool IsConfigured => map.IsConfigured;

    public Redactor? GetRedactor(DataClassificationSet classifications)
    {
        ArgumentNullException.ThrowIfNull(classifications);

        var type = map.RedactorFor(classifications);

        return type is null ? null : (Redactor)services.GetRequiredService(type);
    }
}
