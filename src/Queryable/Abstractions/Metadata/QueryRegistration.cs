using System.Text.Json.Serialization;

namespace Kaleido.Queryable.Metadata;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryContextKind
{
    Local,
    Direct,
    Delegated
}

public sealed record QueryContextRegistration(
    Type ContextType,
    Type SourceType,
    QueryContextMetadata Metadata);

public sealed record QueryContextMetadata
(
    string Name,
    string Description,
    string DisplayName,
    string Version,
    string? Source,
    QueryContextKind Kind,
    PageableMetadata? Pageable,
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

public sealed record QueryViewRegistration
(
    Type QueryViewType, 
    Type ViewType, 
    Type ViewParametersType, 
    Type QueryContextType, 
    QueryViewMetadata Metadata
);

public sealed record DelegatedQueryViewRegistration
(
    Type QueryViewType,
    Type ViewType,
    Type ViewParametersType,
    Type QueryContextType,
    QueryContextMetadata QueryMetadata,
    QueryViewMetadata ViewMetadata
);

public sealed record QueryViewMetadata
(
    string Name,
    string Version,
    string DisplayName,
    string Description,
    QueryViewVisibility Visibility,
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
    IReadOnlyCollection<ConstraintContract> Constraints,
    string? Description);


