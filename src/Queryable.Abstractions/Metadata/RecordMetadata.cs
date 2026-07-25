namespace Kaleido.Queryable.Metadata;

public sealed record RecordMetadata
(
    string Name,
    string? Description,
    string? Version,
    string? Source,
    IReadOnlyList<FieldMetadata> Fields,
    PageableMetadata? Pageable
);
