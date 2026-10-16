using Kaleido.Http.Registry;

namespace Kaleido.Http.Queryable;

// ── Request ──────────────────────────────────────────────────────────────────

/// <summary>
/// The body of a query endpoint for a query without parameters: <c>{"query":{…}}</c>. Used by
/// clients to query a source directly when it takes no parameters.
/// </summary>
/// <param name="Query">The search, filter, sort and page to apply; <see langword="null"/> for none of them.</param>
[ExcludeFromCodeCoverage]
public record QueryApiRequest(
    QueryApiBody? Query = null);

/// <summary>
/// The body of a query endpoint (<c>POST /{service}/queryable/{source}[/{view}]/query</c>):
/// the source's or view's parameters plus an optional query, <c>{"parameters":{…},"query":{…}}</c>.
/// </summary>
/// <typeparam name="TParameters">
/// The parameters record of the source or view: the declared type on the server; on a client, that
/// type or a local mirror.
/// </typeparam>
/// <param name="Parameters">The parameter values; <see langword="null"/> when none are supplied.</param>
/// <param name="Query">The search, filter, sort and page to apply; <see langword="null"/> for none of them.</param>
[ExcludeFromCodeCoverage]
public record QueryApiRequest<TParameters>(
    TParameters? Parameters = null,
    QueryApiBody? Query = null)
    where TParameters : class;

// ── Registry responses ────────────────────────────────────────────────────────

/// <summary>Paging limits on the wire: the default and maximum page size.</summary>
/// <remarks>
/// The registry publishes <see cref="PageableMetadata"/> directly
/// (<see cref="QueryableSourceResponse.Pageable"/>, <see cref="QueryableViewResponse.Pageable"/>);
/// this contract carries only its sizes.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record PageableContract
{
    /// <summary>The page size used when the request does not specify one.</summary>
    public int DefaultSize { get; init; }

    /// <summary>The largest page size a request may ask for.</summary>
    public int MaxSize { get; init; }

    /// <summary>Copies the page sizes of <paramref name="metadata"/>; its default sort field is not carried.</summary>
    /// <param name="metadata">The capability's paging metadata.</param>
    /// <returns>A new <see cref="PageableContract"/> with the same sizes.</returns>
    public static PageableContract FromMetadata(
        PageableMetadata metadata)
    {
        return new PageableContract
        {
            DefaultSize = metadata.DefaultSize,
            MaxSize = metadata.MaxSize,
        };
    }
}

/// <summary>
/// A field of a source's query context as published in the registry
/// (<see cref="QueryableSourceResponse.Fields"/>): its type and which query operations it supports.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableFieldMetadata
{
    /// <summary>The field's name (the query context property name); use it in filter and sort clauses.</summary>
    public required string Name { get; init; }

    /// <summary>The field's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The field's published data type.</summary>
    public required DataTypeDescriptor DataType { get; init; }

    /// <summary>The property's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>), so clients know which values are sensitive; empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];

    /// <summary>True when the field is declared <c>[Filterable]</c> and may appear in a filter condition.</summary>
    public bool IsFilterable { get; init; }

    /// <summary>The operators a filter condition on this field may use; empty when the field is not filterable.</summary>
    public IReadOnlyCollection<FilterOperator> FilterOperators { get; init; }
        = [];

    /// <summary>True when the field is declared <c>[Searchable]</c> and is matched by the request's free-text search.</summary>
    public bool IsSearchable { get; init; }

    /// <summary>
    /// The order in which searchable fields are considered (lower first); <see langword="null"/>
    /// when the field is not searchable.
    /// </summary>
    public int? SearchPriority { get; init; }

    /// <summary>
    /// How the search text is matched against this field; <see langword="null"/> when the field is
    /// not searchable.
    /// </summary>
    public MatchMode? MatchMode { get; init; }

    /// <summary>True when the field is declared <c>[Sortable]</c> and may appear in a sort clause.</summary>
    public bool IsSortable { get; init; }

    /// <summary>Projects a registry field descriptor to its wire shape.</summary>
    /// <param name="item">The field as registered in the Queryable registry.</param>
    /// <returns>A new <see cref="QueryableFieldMetadata"/> with the descriptor's values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public static QueryableFieldMetadata FromRegistryItem(
        QueryableFieldDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableFieldMetadata
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            Classifications = item.Classifications,
            IsFilterable = item.IsFilterable,
            FilterOperators = item.FilterOperators,
            IsSearchable = item.IsSearchable,
            SearchPriority = item.SearchPriority,
            MatchMode = item.MatchMode,
            IsSortable = item.IsSortable,
        };
    }
}

/// <summary>
/// A parameter of a source or view as published in the registry: a property of its parameters
/// record, sent in the request's <c>parameters</c> object.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableQueryParameter
{
    /// <summary>The parameter's name (the parameters record's property name).</summary>
    public required string Name { get; init; }

    /// <summary>The parameter's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The parameter's published data type.</summary>
    public required DataTypeDescriptor DataType { get; init; }

    /// <summary>The parameter's validation constraints, one per validation attribute; empty when it has none.</summary>
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];

    /// <summary>The property's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>), so clients know which values are sensitive; empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];

    /// <summary>Projects a registry parameter descriptor to its wire shape.</summary>
    /// <param name="item">The parameter as registered in the Queryable registry.</param>
    /// <returns>A new <see cref="QueryableQueryParameter"/> with the descriptor's values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public static QueryableQueryParameter FromRegistryItem(
        QueryableParameterDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableQueryParameter
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            Classifications = item.Classifications,
            Constraints = item.Constraints,
        };
    }
}

/// <summary>
/// A field of the records a source or view returns, as published in the registry
/// (<see cref="QueryableSourceResponse.OutputFields"/>, <see cref="QueryableViewResponse.OutputFields"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableQueryProperty
{
    /// <summary>The field's name (the result record's property name).</summary>
    public required string Name { get; init; }

    /// <summary>The field's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The field's published data type.</summary>
    public required DataTypeDescriptor DataType { get; init; }

    /// <summary>The property's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>), so clients know which values are sensitive; empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];

    /// <summary>Projects a registry output field descriptor to its wire shape.</summary>
    /// <param name="item">The output field as registered in the Queryable registry.</param>
    /// <returns>A new <see cref="QueryableQueryProperty"/> with the descriptor's values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public static QueryableQueryProperty FromRegistryItem(
        QueryableOutputFieldDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableQueryProperty
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            Classifications = item.Classifications,
        };
    }
}

/// <summary>
/// A view of a query source as published in the registry (<see cref="QueryableSourceResponse.Views"/>):
/// its description, paging, authorization, query URL, parameters and result fields.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableViewResponse
{
    /// <summary>The view's public name (its type name), unique within its source. Pass it to the client to query the view.</summary>
    public required string Name { get; init; }

    /// <summary>What the view returns.</summary>
    public string? Description { get; init; }

    /// <summary>The human-readable name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The view's version.</summary>
    public string? Version { get; init; }

    /// <summary>Paging for this view (not inherited from its source). Null means not pageable.</summary>
    public PageableMetadata? Pageable { get; init; }

    /// <summary>
    /// Effective authorization requirement for this view
    /// (view-declared or inherited from its source). Null means open.
    /// </summary>
    public AuthorizationMetadata? Authorization { get; init; }

    /// <summary>The URL to query this view: <c>/{service}/queryable/{source}/{view}/query</c>, with lowercase source and view names.</summary>
    public required string QueryUrl { get; init; }

    /// <summary>The parameters the view accepts; empty when it takes none.</summary>
    public IReadOnlyCollection<QueryableQueryParameter> Parameters { get; init; }
        = [];

    /// <summary>The fields of the records the view returns.</summary>
    public IReadOnlyCollection<QueryableQueryProperty> OutputFields { get; init; }
        = [];

    /// <summary>Projects a registry view to its wire shape, building its query URL.</summary>
    /// <param name="item">The view as registered in the Queryable registry.</param>
    /// <param name="sourceName">The owning source's route segment (the lowercase source name), used as-is in <see cref="QueryUrl"/>.</param>
    /// <param name="serviceName">The service's route prefix; when empty the URL has no service segment.</param>
    /// <returns>A new <see cref="QueryableViewResponse"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is <see langword="null"/>, empty or whitespace.</exception>
    public static QueryableViewResponse FromRegistryItem(
        QueryableViewRegistryItem item,
        string sourceName,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        return new QueryableViewResponse
        {
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Version = item.Version,
            Pageable = item.Pageable,
            Authorization = item.Authorization,
            QueryUrl = QueryableContractUrls.QueryViewQuery(
                serviceName,
                sourceName,
                item.Name.ToLowerInvariant()),
            Parameters = item.Parameters
                .Select(QueryableQueryParameter.FromRegistryItem)
                .ToArray(),
            OutputFields = item.OutputFields
                .Select(QueryableQueryProperty.FromRegistryItem)
                .ToArray(),
        };
    }
}

/// <summary>
/// A query source as published in the registry. Every source is presented the same way;
/// how the service fulfils the query is not part of the contract.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableSourceResponse
{
    /// <summary>
    /// The service name — matches <see cref="KaleidoServiceOptions.ServiceName"/>.
    /// Allows consumers to identify which service this source belongs to.
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>The source's public name (its type name). Pass it to the client to query the source.</summary>
    public required string Name { get; init; }

    /// <summary>What the source provides.</summary>
    public string? Description { get; init; }

    /// <summary>The human-readable name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The source's version.</summary>
    public string? Version { get; init; }

    /// <summary>Optional description of where the data comes from.</summary>
    public string? Source { get; init; }

    /// <summary>Paging for direct queries of this source. Null means not pageable.</summary>
    public PageableMetadata? Pageable { get; init; }

    /// <summary>
    /// Authorization requirement for this source. Null means open.
    /// </summary>
    public AuthorizationMetadata? Authorization { get; init; }

    /// <summary>The service's registry URL.</summary>
    public string RegistryUrl { get; init; }
        = string.Empty;

    /// <summary>
    /// The URL to query this source directly. Every source has one; it is <see langword="null"/> only in a
    /// caller-filtered registry when the caller may use some of the source's views but is not
    /// authorized to query the source itself.
    /// </summary>
    public string? QueryUrl { get; init; }

    /// <summary>The fields consumers may search, filter and sort on.</summary>
    public IReadOnlyCollection<QueryableFieldMetadata> Fields { get; init; }
        = [];

    /// <summary>The parameters a direct query of this source accepts.</summary>
    public IReadOnlyCollection<QueryableQueryParameter> Parameters { get; init; }
        = [];

    /// <summary>The fields of the records a direct query returns.</summary>
    public IReadOnlyCollection<QueryableQueryProperty> OutputFields { get; init; }
        = [];

    /// <summary>The source's views (may be empty).</summary>
    public IReadOnlyCollection<QueryableViewResponse> Views { get; init; }
        = [];

    /// <summary>
    /// Projects a registry source, with its fields, parameters, output fields and views, to its wire
    /// shape, building its registry and query URLs (the URL uses the lowercase source name).
    /// </summary>
    /// <param name="item">The source as registered in the Queryable registry.</param>
    /// <param name="serviceName">The service name, published as <see cref="ServiceName"/> and used as the URLs' route prefix.</param>
    /// <returns>A new <see cref="QueryableSourceResponse"/>; <see cref="QueryUrl"/> is always set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public static QueryableSourceResponse FromRegistryItem(
        QueryableSourceRegistryItem item,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(item);

        var sourceName =
            item.Name.ToLowerInvariant();

        return new QueryableSourceResponse
        {
            ServiceName = serviceName,
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Version = item.Version,
            Source = item.Source,
            Pageable = item.Pageable,
            Authorization = item.Authorization,
            RegistryUrl = RegistryContractUrls.Registry(serviceName),
            QueryUrl = QueryableContractUrls.QuerySourceQuery(serviceName, sourceName),
            Fields = item.Fields
                .Select(QueryableFieldMetadata.FromRegistryItem)
                .ToArray(),
            Parameters = item.Parameters
                .Select(QueryableQueryParameter.FromRegistryItem)
                .ToArray(),
            OutputFields = item.OutputFields
                .Select(QueryableQueryProperty.FromRegistryItem)
                .ToArray(),
            Views = item.Views
                .Select(view => QueryableViewResponse.FromRegistryItem(view, sourceName, serviceName))
                .ToArray(),
        };
    }
}
