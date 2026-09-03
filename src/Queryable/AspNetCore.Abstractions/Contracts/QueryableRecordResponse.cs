using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

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
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);

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
            MetadataUrl = QueryableContractUrls.QueryContextMetadata(options, contextName),
            QueryUrl = item.Kind == QueryContextKind.Direct
                ? QueryableContractUrls.QueryContextQuery(options, contextName)
                : null,
            Fields = item.Fields
                .Select(QueryableFieldMetadata.FromRegistryItem)
                .ToArray(),
            Views = item.Views
                .Select(view => QueryableViewResponse.FromRegistryItem(view, contextName, options))
                .ToArray()
        };
    }

    public static QueryableRecordSummary ToSummary(
        QueryableContextRegistryItem item,
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);

        return new QueryableRecordSummary
        {
            Name = item.Name,
            Description = item.Description,
            MetadataUrl = QueryableContractUrls.QueryContextMetadata(
                options,
                item.Name.ToLowerInvariant())
        };
    }
}

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
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);
        ArgumentNullException.ThrowIfNull(options);

        return new QueryableViewResponse
        {
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Version = item.Version,
            Visibility = item.Visibility,
            Pageable = item.Pageable,
            QueryUrl = QueryableContractUrls.QueryViewQuery(
                options,
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
