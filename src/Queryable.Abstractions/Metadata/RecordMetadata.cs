namespace Kaleido.Queryable.Metadata;

public sealed record RecordMetadata
(
    string Name,
    string? Description,
    string? Version,
    string? Source,
    IReadOnlyList<FieldMetadata> Fields,
    IReadOnlyList<NamedQueryMetadata>? NamedQueries,
    PageableMetadata? Pageable
);

public sealed record FieldMetadata
(
    string Name,
    Type FieldType,
    bool IsFilterable,
    IReadOnlyList<FilterOperator> FilterOperators,
    bool IsSearchable,
    int? SearchPriority,
    IReadOnlyList<MatchMode> MatchModes,
    bool IsSortable
)
{
    public bool IsNullable => Nullable.GetUnderlyingType(FieldType) != null;
};

public sealed record NamedQueryMetadata
(
    string Name,
    string Description,
    IReadOnlyList<QueryParameterMetadata>? Parameters
);

public sealed record QueryParameterMetadata(
    string Name,
    Type Type,
    bool Required,
    string Description
);

public sealed record PageableMetadata
(
    int DefaultSize,
    int MaxSize
);

/// <summary>Associates a record key with a record type and runtime metadata.</summary>
public sealed record RecordRegistration(Type RecordType, RecordMetadata Metadata);