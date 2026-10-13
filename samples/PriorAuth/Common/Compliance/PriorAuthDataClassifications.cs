using Microsoft.Extensions.Compliance.Classification;

namespace Kaleido.Samples.PriorAuth.Compliance;

/// <summary>
/// The sample's data classifications. Kaleido redacts values marked with them in event payloads
/// (never in responses to the caller). Without <c>AddRedaction</c> they are erased; a host chooses
/// per classification, e.g. <c>services.AddRedaction(r => r.SetHmacRedactor(..., new DataClassificationSet(PriorAuthDataClassifications.Pii)))</c>.
/// </summary>
public static class PriorAuthDataClassifications
{
    /// <summary>Protected health information: who the member is (name, date of birth).</summary>
    public static DataClassification Phi { get; } = new("PriorAuth", "PHI");

    /// <summary>Personally identifiable contact information (email, phone, address).</summary>
    public static DataClassification Pii { get; } = new("PriorAuth", "PII");
}

/// <summary>Marks protected health information.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PhiAttribute() : DataClassificationAttribute(PriorAuthDataClassifications.Phi);

/// <summary>Marks personally identifiable contact information.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PiiAttribute() : DataClassificationAttribute(PriorAuthDataClassifications.Pii);
