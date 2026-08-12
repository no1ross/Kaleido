using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableRecordResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Version { get; init; }

    public string? Source { get; init; }

    public IReadOnlyCollection<QueryableFieldMetadata> Fields { get; init; }
        = Array.Empty<QueryableFieldMetadata>();

    public IReadOnlyCollection<QueryableViewResponse> Views { get; init; }
        = Array.Empty<QueryableViewResponse>();

    public static QueryableRecordResponse FromRegistration(
        QueryContextRegistration registration,
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var contextName =
            registration.Metadata.Name.ToLowerInvariant();

        return new QueryableRecordResponse
        {
            Name = registration.Metadata.Name,

            Description = registration.Metadata.Description,

            Version = registration.Metadata.Version,

            Source = registration.Metadata.Source,

            Fields = registration.Metadata.Fields
                .Select(QueryableFieldMetadata.FromMetadata)
                .ToArray(),

            //Views = registration.QueryViews
            //    .OrderBy(v => v.Metadata.Name)
            //    .Select(view =>
            //    {
            //        var viewName =
            //            view.Metadata.Name.ToLowerInvariant();

            //        return new QueryableViewResponse
            //        {
            //            Name = view.Metadata.Name,

            //            Description = view.Metadata.Description,

            //            Pageable =
            //                view.Metadata.Pageable is null
            //                    ? null
            //                    : PageableContract.FromMetadata(
            //                        view.Metadata.Pageable),

            //            QueryUrl =
            //                QueryableContractUrls.QueryViewQuery(
            //                    options,
            //                    contextName,
            //                    viewName),

            //            NamedQueries = registration.NamedQueries
            //                .OrderBy(q => q.Metadata.Name)
            //                .Select(query =>
            //                {
            //                    var queryName =
            //                        query.Metadata.Name.ToLowerInvariant();

            //                    return new QueryableNamedQuerySummary
            //                    {
            //                        Name = query.Metadata.Name,

            //                        Description = query.Metadata.Description,

            //                        ExecuteUrl =
            //                            QueryableContractUrls.NamedQuery(
            //                                options,
            //                                contextName,
            //                                viewName,
            //                                queryName),

            //                        MetadataUrl =
            //                            QueryableContractUrls.NamedQueryMetadata(
            //                                options,
            //                                contextName,
            //                                viewName,
            //                                queryName)
            //                    };
            //                })
            //                .ToArray()
            //        };
            //    })
            //    .ToArray()
        };
    }

    public static QueryableRecordSummary ToSummary(
        QueryContextRegistration registration,
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var contextName =
            registration.Metadata.Name.ToLowerInvariant();

        return new QueryableRecordSummary
        {
            Name = registration.Metadata.Name,

            Description = registration.Metadata.Description,

            MetadataUrl =
                QueryableContractUrls.QueryContextMetadata(
                    options,
                    contextName)
        };
    }
}


public sealed record QueryableViewResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public PageableContract? Pageable { get; init; }

    public required string QueryUrl { get; init; }
}