namespace Kaleido.Queryable.Metadata;

public sealed record FieldMetadata
(
    string Name,
    string? Description,
    Type FieldType,
    bool IsFilterable,
    IReadOnlyList<FilterOperator> FilterOperators,
    bool IsSearchable,
    int? SearchPriority,
    IReadOnlyList<MatchMode> MatchModes,
    bool IsSortable
);
