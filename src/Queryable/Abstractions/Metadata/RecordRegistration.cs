namespace Kaleido.Queryable.Metadata;

/// <summary>Associates a record key with a record type and runtime metadata.</summary>
public sealed record RecordRegistration(Type RecordType, Type SourceType, RecordMetadata Metadata, IReadOnlyCollection<NamedQueryRegistration> NamedQueryTypes);

//public sealed record NamedQueryRegistration(Type NamedQueryType, NamedQueryMetadata Metadata);

//public sealed record RecordMetadata
//(
//    string Name,
//    string? Description,
//    string? Version,
//    string? Source,
//    IReadOnlyList<FieldMetadata> Fields,
//    PageableMetadata? Pageable
//);

//public sealed record FieldMetadata
//(
//    string Name,
//    Type FieldType,
//    bool IsFilterable,
//    IReadOnlyList<FilterOperator> FilterOperators,
//    bool IsSearchable,
//    int? SearchPriority,
//    IReadOnlyList<MatchMode> MatchModes,
//    bool IsSortable
//);

//public sealed record PageableMetadata
//(
//    int DefaultSize,
//    int MaxSize
//);

//public sealed record NamedQueryMetadata
//(
//    string Name,
//    string Description,
//    IReadOnlyList<QueryParameterMetadata>? Parameters
//);

//public sealed record QueryParameterMetadata(
//    string Name,
//    Type Type,
//    bool Required,
//    string? Description,
//    object? DefaultValue);