namespace Kaleido.Queryable.Metadata;

internal static class QueryContextMetadataExtensions
{
    public static FieldMetadata GetField(
        this QueryContextMetadata metadata,
        string name) =>
        metadata.Fields.FirstOrDefault(
            x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KaleidoValidationException(
            ValidationErrorCodes.QryInvalidField,
            $"Field '{name}' does not exist on record '{metadata.Name}'.");
}
