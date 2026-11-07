using Microsoft.Extensions.Compliance.Classification;

namespace Kaleido.Http.FunctionalTests.Queryable.Infrastructure;

// A test classification: values marked with it are redacted in event payloads (never in responses).
[AttributeUsage(AttributeTargets.Property)]
public sealed class FunctionalSensitiveDataAttribute() : DataClassificationAttribute(Sensitive)
{
    public static readonly DataClassification Sensitive = new("functional", "sensitive");
}
