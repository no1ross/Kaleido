namespace Kaleido.Queryable.Metadata;

public sealed record QueryContextRegistration(Type ContextType, Type SourceType, QueryContextMetadata Metadata);

public sealed record QueryContextMetadata
(
    string Name,
    string Description,
    string DisplayName,
    string Version,
    string? Source,
    IReadOnlyList<FieldMetadata> Fields
);

public sealed record FieldMetadata
(
    string Name,
    string? Description,
    Type FieldType,
    bool IsFilterable,
    IReadOnlyList<FilterOperator> FilterOperators,
    bool IsSearchable,
    int? SearchPriority,
    MatchMode? MatchMode,
    bool IsSortable
);

public sealed record NamedQueryMetadata
(
    string Name,
    string Version,
    string DisplayName,
    string Description,
    IReadOnlyList<QueryParameterMetadata>? Parameters
);

public sealed record QueryViewRegistration
(
    Type QueryViewType, 
    Type ViewType, 
    Type ViewParametersType, 
    Type QueryContextType, 
    QueryViewMetadata Metadata
);

public sealed record QueryViewMetadata
(
    string Name,
    string Version,
    string DisplayName,
    string Description,
    PageableMetadata? Pageable,
    IReadOnlyList<QueryParameterMetadata>? Parameters
);

public sealed record PageableMetadata
(
    int DefaultSize,
    int MaxSize
);

public sealed record QueryParameterMetadata(
    string Name,
    Type Type,
    bool Required,
    string? Description,
    object? DefaultValue);


