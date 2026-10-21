using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Eventing;

// The IRedactionBuilder AddEventClassificationRedaction hands to the host: the standard API, so Microsoft's
// extensions (SetHmacRedactor...) work in it, but writing to the event policy, never to the host's
// log redaction. Redactor types are registered as singletons, as AddRedaction does.
internal sealed class KaleidoEventRedactionBuilder(IServiceCollection services, KaleidoEventRedactionMap map)
    : IRedactionBuilder
{
    public IServiceCollection Services { get; } = services;

    public IRedactionBuilder SetRedactor<T>(params DataClassificationSet[] classifications)
        where T : Redactor
    {
        ArgumentNullException.ThrowIfNull(classifications);

        Services.TryAddSingleton<T>();

        foreach (var classification in classifications)
        {
            map.SetRedactor(classification, typeof(T));
        }

        return this;
    }

    public IRedactionBuilder SetFallbackRedactor<T>()
        where T : Redactor
    {
        Services.TryAddSingleton<T>();
        map.Fallback = typeof(T);

        return this;
    }
}
