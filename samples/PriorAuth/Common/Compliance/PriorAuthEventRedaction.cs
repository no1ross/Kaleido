using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace Kaleido.Samples.PriorAuth.Compliance;

/// <summary>The sample's event redaction policy, shared by every PriorAuth service that publishes events.</summary>
public static class PriorAuthEventRedaction
{
    /// <summary>
    /// Events keep PII (contact details the downstream systems need) and erase PHI (no redactor is set
    /// for it, so it is erased). Log redaction is separate: this policy applies to events only (ADR 0025).
    /// </summary>
    public static IKaleidoBuilder AddPriorAuthEventRedaction(this IKaleidoBuilder builder) =>
        builder.AddEventRedaction(r =>
            r.SetRedactor<NullRedactor>(new DataClassificationSet(PriorAuthDataClassifications.Pii)));
}
