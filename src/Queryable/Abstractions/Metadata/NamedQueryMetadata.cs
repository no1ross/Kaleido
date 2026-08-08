namespace Kaleido.Queryable.Metadata;

public sealed record NamedQueryMetadata
(
    string Name,
    string DisplayName,
    string Description,
    IReadOnlyList<QueryParameterMetadata>? Parameters
);
