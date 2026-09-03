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
    DataTypeDescriptor DataType,
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
    IReadOnlyList<QueryParameterMetadata>? Parameters,
    IReadOnlyList<QueryOutputFieldMetadata>? OutputFields
);

public sealed record PageableMetadata
(
    int DefaultSize,
    int MaxSize
);

public sealed record QueryParameterMetadata(
    string Name,
    Type Type,
    DataTypeDescriptor DataType,
    IReadOnlyCollection<ConstraintContract> Constraints,
    string? Description);

public sealed record QueryOutputFieldMetadata(
    string Name,
    string? Description,
    Type Type,
    DataTypeDescriptor DataType);

public record QueryableContextRegistryItem
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public string? Source { get; init; }

    public required QueryContextKind Kind { get; init; }

    public PageableMetadata? Pageable { get; init; }

    public IReadOnlyCollection<QueryableFieldDescriptor> Fields { get; init; }
        = [];

    public IReadOnlyCollection<QueryableViewRegistryItem> Views { get; init; }
        = [];
}

public record QueryableViewRegistryItem
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public required QueryViewVisibility Visibility { get; init; }

    public PageableMetadata? Pageable { get; init; }

    public IReadOnlyCollection<QueryableParameterDescriptor> Parameters { get; init; }
        = [];

    public IReadOnlyCollection<QueryableOutputFieldDescriptor> OutputFields { get; init; }
        = [];
}

public record QueryablePropertyDescriptor
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DataTypeDescriptor DataType { get; init; }
}

public record QueryableFieldDescriptor : QueryablePropertyDescriptor
{
    public bool IsFilterable { get; init; }

    public IReadOnlyCollection<FilterOperator> FilterOperators { get; init; }
        = [];

    public bool IsSearchable { get; init; }

    public int? SearchPriority { get; init; }

    public MatchMode? MatchMode { get; init; }

    public bool IsSortable { get; init; }
}

public record QueryableParameterDescriptor : QueryablePropertyDescriptor
{
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];
}

public record QueryableOutputFieldDescriptor : QueryablePropertyDescriptor;


