using Kaleido.Registry;

namespace Kaleido.Queryable.Metadata;

public enum QueryContextKind
{
    Local,
    Direct,
    Delegated
}

[ExcludeFromCodeCoverage]
public sealed record QueryContextRegistration(
    Type ContextType,
    Type SourceType,
    QueryContextMetadata Metadata);

[ExcludeFromCodeCoverage]
public sealed record QueryContextMetadata
(
    string Name,
    string Description,
    string DisplayName,
    string Version,
    string? Source,
    QueryContextKind Kind,
    PageableMetadata? Pageable,
    IReadOnlyList<FieldMetadata> Fields,
    AuthorizationMetadata Authorization
)
{
    public QueryContextMetadata(
        string name, string description, string displayName, string version,
        string? source, QueryContextKind kind, PageableMetadata? pageable,
        IReadOnlyList<FieldMetadata> fields)
        : this(name, description, displayName, version, source, kind, pageable,
            fields, AuthorizationMetadata.Unspecified)
    {
    }
}

[ExcludeFromCodeCoverage]
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

[ExcludeFromCodeCoverage]
public sealed record QueryViewRegistration
(
    Type QueryViewType,
    Type ViewType,
    Type ViewParametersType,
    Type QueryContextType,
    QueryViewMetadata Metadata
);

[ExcludeFromCodeCoverage]
public sealed record DelegatedQueryViewRegistration
(
    Type QueryViewType,
    Type ViewType,
    Type ViewParametersType,
    Type QueryContextType,
    QueryContextMetadata QueryMetadata,
    QueryViewMetadata ViewMetadata
);

[ExcludeFromCodeCoverage]
public sealed record QueryViewMetadata
(
    string Name,
    string Version,
    string DisplayName,
    string Description,
    PageableMetadata? Pageable,
    IReadOnlyList<QueryParameterMetadata>? Parameters,
    IReadOnlyList<QueryOutputFieldMetadata>? OutputFields,
    AuthorizationMetadata? Authorization = null
);

[ExcludeFromCodeCoverage]
public sealed record PageableMetadata
(
    int DefaultSize,
    int MaxSize
);

[ExcludeFromCodeCoverage]
public sealed record QueryParameterMetadata(
    string Name,
    Type Type,
    DataTypeDescriptor DataType,
    IReadOnlyCollection<ConstraintContract> Constraints,
    string? Description);

[ExcludeFromCodeCoverage]
public sealed record QueryOutputFieldMetadata(
    string Name,
    string? Description,
    Type Type,
    DataTypeDescriptor DataType);

[ExcludeFromCodeCoverage]
public record QueryableContextRegistryItem
{
    /// <summary>
    /// The CLR type of the query context. For transport endpoint mapping (e.g. MakeGenericMethod).
    /// Not serialized to the wire.
    /// </summary>
    public required Type ContextType { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public string? Source { get; init; }

    public required QueryContextKind Kind { get; init; }

    public PageableMetadata? Pageable { get; init; }

    /// <summary>
    /// Effective authorization rule. <see cref="AuthorizationMetadata.Unspecified"/>
    /// means neither the capability nor its service declared a rule.
    /// </summary>
    public AuthorizationMetadata Authorization { get; init; } = AuthorizationMetadata.Unspecified;

    public IReadOnlyCollection<QueryableFieldDescriptor> Fields { get; init; }
        = [];

    public IReadOnlyCollection<QueryableViewRegistryItem> Views { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
public record QueryableViewRegistryItem
{
    /// <summary>
    /// The CLR type of the query view class. For transport endpoint mapping (e.g. MakeGenericMethod).
    /// Not serialized to the wire.
    /// </summary>
    public required Type QueryViewType { get; init; }

    /// <summary>
    /// The CLR type of the view result. For transport endpoint mapping.
    /// Not serialized to the wire.
    /// </summary>
    public required Type ViewType { get; init; }

    /// <summary>
    /// The CLR type of the view parameters. For transport endpoint mapping.
    /// Not serialized to the wire.
    /// </summary>
    public required Type ViewParametersType { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public PageableMetadata? Pageable { get; init; }

    /// <summary>
    /// Effective authorization requirement — the view's own
    /// <c>[KaleidoAuthorization]</c>, or the owning context's when the view
    /// declares none. <see cref="AuthorizationMetadata.Unspecified"/> means
    /// no declaration exists at either level.
    /// </summary>
    public AuthorizationMetadata Authorization { get; init; } = AuthorizationMetadata.Unspecified;

    public IReadOnlyCollection<QueryableParameterDescriptor> Parameters { get; init; }
        = [];

    public IReadOnlyCollection<QueryableOutputFieldDescriptor> OutputFields { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
public record QueryablePropertyDescriptor
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// The CLR type of this property. Transport layers use this for value
    /// resolution and OpenAPI schema generation. Not serialized to the wire.
    /// </summary>
    public required Type FieldType { get; init; }

    public required DataTypeDescriptor DataType { get; init; }
}

[ExcludeFromCodeCoverage]
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

[ExcludeFromCodeCoverage]
public record QueryableParameterDescriptor : QueryablePropertyDescriptor
{
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
public record QueryableOutputFieldDescriptor : QueryablePropertyDescriptor;

