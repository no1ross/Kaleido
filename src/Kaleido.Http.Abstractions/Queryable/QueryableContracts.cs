using Kaleido.Http.Registry;

namespace Kaleido.Http.Queryable;

// ── Request ──────────────────────────────────────────────────────────────────

[ExcludeFromCodeCoverage]
public record QueryApiRequest(
    QueryApiBody? Query = null);

[ExcludeFromCodeCoverage]
public record QueryApiRequest<TParameters>(
    TParameters? Parameters = null,
    QueryApiBody? Query = null)
    where TParameters : class;

// ── Registry responses ────────────────────────────────────────────────────────

[ExcludeFromCodeCoverage]
public sealed record PageableContract
{
    public int DefaultSize { get; init; }

    public int MaxSize { get; init; }

    public static PageableContract FromMetadata(
        PageableMetadata metadata)
    {
        return new PageableContract
        {
            DefaultSize = metadata.DefaultSize,
            MaxSize = metadata.MaxSize
        };
    }
}

[ExcludeFromCodeCoverage]
public sealed record QueryableFieldMetadata
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    public bool IsFilterable { get; init; }

    public IReadOnlyCollection<FilterOperator> FilterOperators { get; init; }
        = [];

    public bool IsSearchable { get; init; }

    public int? SearchPriority { get; init; }

    public MatchMode? MatchMode { get; init; }

    public bool IsSortable { get; init; }

    public static QueryableFieldMetadata FromRegistryItem(
        QueryableFieldDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableFieldMetadata
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            IsFilterable = item.IsFilterable,
            FilterOperators = item.FilterOperators,
            IsSearchable = item.IsSearchable,
            SearchPriority = item.SearchPriority,
            MatchMode = item.MatchMode,
            IsSortable = item.IsSortable
        };
    }
}

[ExcludeFromCodeCoverage]
public sealed record QueryableQueryParameter
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];

    public static QueryableQueryParameter FromRegistryItem(
        QueryableParameterDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableQueryParameter
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            Constraints = item.Constraints
        };
    }
}

[ExcludeFromCodeCoverage]
public sealed record QueryableQueryProperty
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    public static QueryableQueryProperty FromRegistryItem(
        QueryableOutputFieldDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableQueryProperty
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType
        };
    }
}

[ExcludeFromCodeCoverage]
public sealed record QueryableViewResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public PageableMetadata? Pageable { get; init; }

    /// <summary>
    /// Effective authorization requirement for this view
    /// (view-declared or inherited from its source). Null means open.
    /// </summary>
    public AuthorizationMetadata? Authorization { get; init; }

    public required string QueryUrl { get; init; }

    public IReadOnlyCollection<QueryableQueryParameter> Parameters { get; init; }
        = [];

    public IReadOnlyCollection<QueryableQueryProperty> OutputFields { get; init; }
        = [];

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
                .ToArray()
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
    /// The URL to query this source directly. Every source has one; it is <c>null</c> only in a
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
                .ToArray()
        };
    }
}
