using Kaleido.Registry;

namespace Kaleido.Queryable.Metadata;

/// <summary>
/// How a query source produces its results. Derived from the interface the source implements;
/// it is never declared by the developer.
/// </summary>
/// <remarks>
/// This is runtime metadata for the service and its developers (available to sources and views
/// through <c>QueryExecutionContext.Metadata</c>). It is not published to consumers: the registry
/// and transports present every source the same way.
/// </remarks>
public enum QuerySourceKind
{
    /// <summary>
    /// A local source (<see cref="IQuerySource{TQueryContext}"/> or
    /// <see cref="IQuerySourceAsync{TQueryContext}"/>): Kaleido applies search, filter, sort and
    /// paging in-process. It can be queried directly and through its views.
    /// </summary>
    Local,

    /// <summary>
    /// A delegated source (<see cref="IDelegatedQuerySource{TQueryContext,TResult,TParameters}"/>):
    /// the source executes the query downstream and returns its own mapped results. It has no views.
    /// </summary>
    Delegated
}

/// <summary>A discovered query source and its metadata.</summary>
/// <param name="SourceType">The source type (the capability's identity).</param>
/// <param name="QueryContextType">The source's query context record.</param>
/// <param name="ResultType">
/// The record type a direct query returns: the query context for a local source, or the source's own
/// result record for a delegated source.
/// </param>
/// <param name="ParametersType">The source's parameters record (<see cref="EmptyQueryViewParameters"/> for local sources).</param>
/// <param name="Metadata">The source's published metadata.</param>
[ExcludeFromCodeCoverage]
public sealed record QuerySourceRegistration(
    Type SourceType,
    Type QueryContextType,
    Type ResultType,
    Type ParametersType,
    QuerySourceMetadata Metadata);

/// <summary>Published metadata for a query source.</summary>
/// <param name="Name">The public name (the source's type name).</param>
/// <param name="Description">What the source provides.</param>
/// <param name="DisplayName">The human-readable name.</param>
/// <param name="Version">The source's version.</param>
/// <param name="Source">Optional description of where the data comes from.</param>
/// <param name="Kind">Local or delegated (derived).</param>
/// <param name="Pageable">Paging for direct queries of this source; <c>null</c> when not pageable.</param>
/// <param name="Fields">The query context fields and their query rules.</param>
/// <param name="Parameters">The source's parameters (delegated sources only; empty otherwise).</param>
/// <param name="OutputFields">The fields of the record a direct query returns.</param>
/// <param name="Authorization">The effective authorization rule.</param>
[ExcludeFromCodeCoverage]
public sealed record QuerySourceMetadata(
    string Name,
    string Description,
    string DisplayName,
    string Version,
    string? Source,
    QuerySourceKind Kind,
    PageableMetadata? Pageable,
    IReadOnlyList<FieldMetadata> Fields,
    IReadOnlyList<QueryParameterMetadata> Parameters,
    IReadOnlyList<QueryOutputFieldMetadata> OutputFields,
    AuthorizationMetadata Authorization);

/// <summary>A query context field and its query rules.</summary>
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

/// <summary>A discovered local query view and its metadata.</summary>
/// <param name="QueryViewType">The view type (the capability's identity).</param>
/// <param name="ViewType">The record type the view returns.</param>
/// <param name="ViewParametersType">The view's parameters record.</param>
/// <param name="SourceType">The local source the view projects.</param>
/// <param name="QueryContextType">The source's query context record.</param>
/// <param name="Metadata">The view's published metadata.</param>
[ExcludeFromCodeCoverage]
public sealed record QueryViewRegistration
(
    Type QueryViewType,
    Type ViewType,
    Type ViewParametersType,
    Type SourceType,
    Type QueryContextType,
    QueryViewMetadata Metadata
);

/// <summary>Published metadata for a local query view.</summary>
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

/// <summary>Paging limits.</summary>
[ExcludeFromCodeCoverage]
public sealed record PageableMetadata
(
    int DefaultSize,
    int MaxSize
);

/// <summary>A query parameter and its constraints.</summary>
[ExcludeFromCodeCoverage]
public sealed record QueryParameterMetadata(
    string Name,
    Type Type,
    DataTypeDescriptor DataType,
    IReadOnlyCollection<ConstraintContract> Constraints,
    string? Description);

/// <summary>A field of a returned record.</summary>
[ExcludeFromCodeCoverage]
public sealed record QueryOutputFieldMetadata(
    string Name,
    string? Description,
    Type Type,
    DataTypeDescriptor DataType);

/// <summary>
/// A query source as published in the Queryable registry, with its views. Every source is
/// published the same way: how it fulfils queries (<see cref="QuerySourceKind"/>) is an
/// implementation detail of the service and is deliberately not published.
/// </summary>
[ExcludeFromCodeCoverage]
public record QueryableSourceRegistryItem
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

    /// <summary>The source's version.</summary>
    public string? Version { get; init; }

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

    /// <summary>The public name (the view's type name), unique within its source.</summary>
    public required string Name { get; init; }

    /// <summary>What the view returns.</summary>
    public string? Description { get; init; }

    /// <summary>The human-readable name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The view's version.</summary>
    public string? Version { get; init; }

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
public record QueryablePropertyDescriptor
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
}

/// <summary>A published query context field with its query rules.</summary>
[ExcludeFromCodeCoverage]
public record QueryableFieldDescriptor : QueryablePropertyDescriptor
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
public record QueryableParameterDescriptor : QueryablePropertyDescriptor
{
    /// <summary>The parameter's validation constraints.</summary>
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];
}

/// <summary>A published field of a returned record.</summary>
[ExcludeFromCodeCoverage]
public record QueryableOutputFieldDescriptor : QueryablePropertyDescriptor;
