using Kaleido.Registry;

namespace Kaleido.Queryable.Metadata;

/// <summary>
/// A query source as published in the Queryable registry, with its views. Every source is
/// published the same way: how it fulfils queries (<see cref="QuerySourceKind"/>) is an
/// implementation detail of the service and is deliberately not published.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record QueryableSourceRegistryItem
{
    /// <summary>The source type. For transport endpoint mapping. Not serialized to the wire.</summary>
    public required Type SourceType { get; init; }

    /// <summary>The source's query context record. For transport value resolution. Not serialized to the wire.</summary>
    public required Type QueryContextType { get; init; }

    /// <summary>The record type a direct query returns. For transport endpoint mapping. Not serialized to the wire.</summary>
    public required Type ResultType { get; init; }

    /// <summary>The source's parameters record. For transport endpoint mapping. Not serialized to the wire.</summary>
    public required Type ParametersType { get; init; }

    /// <summary>The public name (the source's type name).</summary>
    public required string Name { get; init; }

    /// <summary>What the source provides.</summary>
    public string? Description { get; init; }

    /// <summary>The human-readable name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Optional description of where the data comes from.</summary>
    public string? Source { get; init; }

    /// <summary>Paging for direct queries of this source.</summary>
    public PageableMetadata? Pageable { get; init; }

    /// <summary>
    /// Effective authorization rule. <see cref="AuthorizationMetadata.Unspecified"/>
    /// means neither the capability nor its service declared a rule.
    /// </summary>
    public AuthorizationMetadata Authorization { get; init; } = AuthorizationMetadata.Unspecified;

    /// <summary>The query context fields and their query rules.</summary>
    public IReadOnlyCollection<QueryableFieldDescriptor> Fields { get; init; }
        = [];

    /// <summary>The source's parameters (delegated sources only).</summary>
    public IReadOnlyCollection<QueryableParameterDescriptor> Parameters { get; init; }
        = [];

    /// <summary>The fields of the record a direct query returns.</summary>
    public IReadOnlyCollection<QueryableOutputFieldDescriptor> OutputFields { get; init; }
        = [];

    /// <summary>The source's local views (always empty for delegated sources).</summary>
    public IReadOnlyCollection<QueryableViewRegistryItem> Views { get; init; }
        = [];
}

/// <summary>A local query view as published in the Queryable registry.</summary>
[ExcludeFromCodeCoverage]
internal sealed record QueryableViewRegistryItem
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

    /// <summary>The public name (the view's type name), unique within its source.</summary>
    public required string Name { get; init; }

    /// <summary>What the view returns.</summary>
    public string? Description { get; init; }

    /// <summary>The human-readable name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Paging for this view.</summary>
    public PageableMetadata? Pageable { get; init; }

    /// <summary>
    /// Effective authorization requirement — the view's own
    /// <c>[KaleidoAuthorization]</c>, or the owning source's when the view
    /// declares none. <see cref="AuthorizationMetadata.Unspecified"/> means
    /// no declaration exists at either level.
    /// </summary>
    public AuthorizationMetadata Authorization { get; init; } = AuthorizationMetadata.Unspecified;

    /// <summary>The view's parameters.</summary>
    public IReadOnlyCollection<QueryableParameterDescriptor> Parameters { get; init; }
        = [];

    /// <summary>The fields of the view's records.</summary>
    public IReadOnlyCollection<QueryableOutputFieldDescriptor> OutputFields { get; init; }
        = [];
}

/// <summary>A published property.</summary>
[ExcludeFromCodeCoverage]
internal record QueryablePropertyDescriptor
{
    /// <summary>The property name.</summary>
    public required string Name { get; init; }

    /// <summary>The property description.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The CLR type of this property. Transport layers use this for value
    /// resolution and OpenAPI schema generation. Not serialized to the wire.
    /// </summary>
    public required Type FieldType { get; init; }

    /// <summary>The published data type.</summary>
    public required DataTypeDescriptor DataType { get; init; }

    /// <summary>The property's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>), so clients know which values are sensitive; empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

/// <summary>A published query context field with its query rules.</summary>
[ExcludeFromCodeCoverage]
internal sealed record QueryableFieldDescriptor : QueryablePropertyDescriptor
{
    /// <summary>Whether the field can be filtered.</summary>
    public bool IsFilterable { get; init; }

    /// <summary>The allowed filter operators.</summary>
    public IReadOnlyCollection<FilterOperator> FilterOperators { get; init; }
        = [];

    /// <summary>Whether the field is searched by free text.</summary>
    public bool IsSearchable { get; init; }

    /// <summary>The field's search priority.</summary>
    public int? SearchPriority { get; init; }

    /// <summary>How search text is matched.</summary>
    public MatchMode? MatchMode { get; init; }

    /// <summary>Whether the field can be sorted.</summary>
    public bool IsSortable { get; init; }
}

/// <summary>A published query parameter with its constraints.</summary>
[ExcludeFromCodeCoverage]
internal sealed record QueryableParameterDescriptor : QueryablePropertyDescriptor
{
    /// <summary>The parameter's validation constraints.</summary>
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];
}

/// <summary>A published field of a returned record.</summary>
[ExcludeFromCodeCoverage]
internal sealed record QueryableOutputFieldDescriptor : QueryablePropertyDescriptor;
