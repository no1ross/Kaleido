namespace Kaleido.Queryable.Metadata;

public sealed record NamedQueryMetadata
(
    string Name,
    string Description,
    IReadOnlyList<QueryParameterMetadata>? Parameters
);
