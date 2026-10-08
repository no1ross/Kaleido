namespace Kaleido.Queryable.Metadata;

internal static class QuerySourceMetadataExtensions
{
    public static FieldMetadata GetField(
        this QuerySourceMetadata metadata,
        string name) =>
        metadata.Fields.FirstOrDefault(
            x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KaleidoValidationException(
            QueryableErrorCodes.InvalidField,
            $"Field '{name}' does not exist on query source '{metadata.Name}'.");
}
