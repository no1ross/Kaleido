namespace Kaleido.Registry;

// ── Registry responses ────────────────────────────────────────────────────────

/// <summary>
/// A field of a source's query context as published in the registry
/// (<see cref="QueryableRegistryItem.Fields"/>): its type and which query operations it supports.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableRegistryField
{
    /// <summary>The field's name (the query context property name); use it in filter and sort clauses.</summary>
    public required string Name { get; init; }

    /// <summary>The field's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The field's published data type.</summary>
    public required DataTypeMetadata DataType { get; init; }

    /// <summary>The property's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>), so clients know which values are sensitive; empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];

    /// <summary>True when the field is declared <c>[Filterable]</c> and may appear in a filter condition.</summary>
    public bool IsFilterable { get; init; }

    /// <summary>The operators a filter condition on this field may use; empty when the field is not filterable.</summary>
    public IReadOnlyCollection<string> FilterOperators { get; init; }
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
    public string? MatchMode { get; init; }

    /// <summary>True when the field is declared <c>[Sortable]</c> and may appear in a sort clause.</summary>
    public bool IsSortable { get; init; }

}

/// <summary>
/// A parameter of a source or view as published in the registry: a property of its parameters
/// record, sent in the request's <c>parameters</c> object.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableRegistryParameter
{
    /// <summary>The parameter's name (the parameters record's property name).</summary>
    public required string Name { get; init; }

    /// <summary>The parameter's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The parameter's published data type.</summary>
    public required DataTypeMetadata DataType { get; init; }

    /// <summary>The parameter's validation constraints, one per validation attribute; empty when it has none.</summary>
    public IReadOnlyCollection<ConstraintMetadata> Constraints { get; init; }
        = [];

    /// <summary>The property's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>), so clients know which values are sensitive; empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];

}

/// <summary>
/// A field of the records a source or view returns, as published in the registry
/// (<see cref="QueryableRegistryItem.OutputFields"/>, <see cref="QueryableRegistryViewItem.OutputFields"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableRegistryOutputField
{
    /// <summary>The field's name (the result record's property name).</summary>
    public required string Name { get; init; }

    /// <summary>The field's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The field's published data type.</summary>
    public required DataTypeMetadata DataType { get; init; }

    /// <summary>The property's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>), so clients know which values are sensitive; empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];

}

/// <summary>
/// A view of a query source as published in the registry (<see cref="QueryableRegistryItem.Views"/>):
/// its description, paging, authorization, transport bindings, parameters and result fields.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableRegistryViewItem
{
    /// <summary>The view's public name (its type name), unique within its source. Pass it to the client to query the view.</summary>
    public required string Name { get; init; }

    /// <summary>What the view returns.</summary>
    public string? Description { get; init; }

    /// <summary>The human-readable name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Paging for this view (not inherited from its source). Null means not pageable.</summary>
    public QueryableRegistryPaging? Pageable { get; init; }

    /// <summary>
    /// Effective authorization requirement for this view
    /// (view-declared or inherited from its source). Null means open.
    /// </summary>
    public AuthorizationMetadata? Authorization { get; init; }

    /// <summary>The operations exposed for this view by its installed transports.</summary>
    public RegistryTransportBindings Bindings { get; init; } = new();

    /// <summary>The parameters the view accepts; empty when it takes none.</summary>
    public IReadOnlyCollection<QueryableRegistryParameter> Parameters { get; init; }
        = [];

    /// <summary>The fields of the records the view returns.</summary>
    public IReadOnlyCollection<QueryableRegistryOutputField> OutputFields { get; init; }
        = [];

}

/// <summary>
/// A query source as published in the registry. Every source is presented the same way;
/// how the service fulfils the query is not part of the contract.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryableRegistryItem
{
    /// <summary>
    /// The service name — matches <see cref="KaleidoServiceOptions.ServiceName"/>.
    /// Allows consumers to identify which service this source belongs to.
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// The service's version (<see cref="KaleidoServiceOptions.ServiceVersion"/>); <see langword="null"/>
    /// when the service is unversioned. The control registry keeps one entry per service and version (#553).
    /// </summary>
    public string? ServiceVersion { get; init; }

    /// <summary>
    /// The capability's overall status: of its service version, from its instances (#553). Not reported on
    /// a host that reads no control registry.
    /// </summary>
    public string Status { get; init; } = RegistryStatusNames.NotReported;

    /// <summary>The source's public name (its type name). Pass it to the client to query the source.</summary>
    public required string Name { get; init; }

    /// <summary>What the source provides.</summary>
    public string? Description { get; init; }

    /// <summary>The human-readable name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Optional description of where the data comes from.</summary>
    public string? Source { get; init; }

    /// <summary>Paging for direct queries of this source. Null means not pageable.</summary>
    public QueryableRegistryPaging? Pageable { get; init; }

    /// <summary>
    /// Authorization requirement for this source. Null means open.
    /// </summary>
    public AuthorizationMetadata? Authorization { get; init; }

    /// <summary>The operations exposed for this source by its installed transports.</summary>
    public RegistryTransportBindings Bindings { get; init; } = new();

    /// <summary>The fields consumers may search, filter and sort on.</summary>
    public IReadOnlyCollection<QueryableRegistryField> Fields { get; init; }
        = [];

    /// <summary>The parameters a direct query of this source accepts.</summary>
    public IReadOnlyCollection<QueryableRegistryParameter> Parameters { get; init; }
        = [];

    /// <summary>The fields of the records a direct query returns.</summary>
    public IReadOnlyCollection<QueryableRegistryOutputField> OutputFields { get; init; }
        = [];

    /// <summary>The source's views (may be empty).</summary>
    public IReadOnlyCollection<QueryableRegistryViewItem> Views { get; init; }
        = [];

}

/// <summary>
/// A source's or view's paging (<see cref="QueryableRegistryItem.Pageable"/>,
/// <see cref="QueryableRegistryViewItem.Pageable"/>): its page sizes, and the order pages follow when the
/// request specifies no sort.
/// </summary>
/// <param name="DefaultSize">The page size used when the request does not specify one.</param>
/// <param name="MaxSize">The largest page size a request may ask for.</param>
/// <param name="DefaultSortField">
/// The field a request without a sort is ordered by (ascending), so pages are stable;
/// <see langword="null"/> when the order is the service's own.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record QueryableRegistryPaging(
    int DefaultSize,
    int MaxSize,
    string? DefaultSortField = null);
