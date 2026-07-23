using Kaleido.Queryable;

namespace Kaleido.Queryable.Metadata;

public sealed record RecordMetadata
(
    string Name,
    string Version,
    string Source,
    IReadOnlyList<FieldMetadata> Fields,
    IReadOnlyList<AllowedQueryMetadata>? AllowedQueries,
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
    bool IsSortable,
    IReadOnlyList<SortDirection> SortDirections
);

public sealed record AllowedQueryMetadata
(
    string Name,
    string Description,
    IReadOnlyList<string> Parameters
);

public sealed record PageableMetadata
(
    int DefaultSize,
    int MaxSize,
    bool CursorSupported
);
