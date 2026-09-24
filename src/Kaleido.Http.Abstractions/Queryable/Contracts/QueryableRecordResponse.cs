namespace Kaleido.Http.Queryable.Contracts;

[ExcludeFromCodeCoverage]
public sealed record QueryableRecordResponse : QueryableContextRegistryItem
{
    public required string MetadataUrl { get; init; }

    public string? QueryUrl { get; init; }

    public new IReadOnlyCollection<QueryableFieldMetadata> Fields { get; init; }
        = [];

    public new IReadOnlyCollection<QueryableViewResponse> Views { get; init; }
        = [];

    public static QueryableRecordResponse FromRegistryItem(
        QueryableContextRegistryItem item,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(item);

        var contextName =
            item.Name.ToLowerInvariant();

        return new QueryableRecordResponse
        {
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Version = item.Version,
            Source = item.Source,
            Kind = item.Kind,
            Pageable = item.Pageable,
            MetadataUrl = QueryableContractUrls.QueryContextMetadata(serviceName, contextName),
            QueryUrl = item.Kind == QueryContextKind.Direct
                ? QueryableContractUrls.QueryContextQuery(serviceName, contextName)
                : null,
            Fields = item.Fields
                .Select(QueryableFieldMetadata.FromRegistryItem)
                .ToArray(),
            Views = item.Views
                .Select(view => QueryableViewResponse.FromRegistryItem(view, contextName, serviceName))
                .ToArray()
        };
    }

    public static QueryableRecordSummary ToSummary(
        QueryableContextRegistryItem item,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableRecordSummary
        {
            Name = item.Name,
            Description = item.Description,
            MetadataUrl = QueryableContractUrls.QueryContextMetadata(
                serviceName,
                item.Name.ToLowerInvariant())
        };
    }
}

[ExcludeFromCodeCoverage]
public sealed record QueryableViewResponse : QueryableViewRegistryItem
{
    public required string QueryUrl { get; init; }

    public new IReadOnlyCollection<QueryableQueryParameter> Parameters { get; init; }
        = [];

    public new IReadOnlyCollection<QueryableQueryProperty> OutputFields { get; init; }
        = [];

    public static QueryableViewResponse FromRegistryItem(
        QueryableViewRegistryItem item,
        string contextName,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);

        return new QueryableViewResponse
        {
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Version = item.Version,
            Visibility = item.Visibility,
            Pageable = item.Pageable,
            QueryUrl = QueryableContractUrls.QueryViewQuery(
                serviceName,
                contextName,
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

[ExcludeFromCodeCoverage]
public sealed record QueryableQueryProperty : QueryableOutputFieldDescriptor
{
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
