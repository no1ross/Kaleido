using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableRecordResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Version { get; init; }

    public string? Source { get; init; }

    public PageableContract? Pageable { get; init; }

    public IReadOnlyCollection<QueryableFieldMetadata> Fields { get; init; }
        = Array.Empty<QueryableFieldMetadata>();

    public IReadOnlyCollection<QueryableNamedQuerySummary> NamedQueries { get; init; }
        = Array.Empty<QueryableNamedQuerySummary>();

    public static QueryableRecordResponse FromRegistration(
        RecordRegistration registration,
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new QueryableRecordResponse
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            Version = registration.Metadata.Version,
            Source = registration.Metadata.Source,
            Pageable = registration.Metadata.Pageable is null
                ? null
                : PageableContract.FromMetadata(
                    registration.Metadata.Pageable),
            Fields = registration.Metadata.Fields
                .Select(QueryableFieldMetadata.FromMetadata)
                .ToArray(),
            NamedQueries = registration.NamedQueryTypes
            .OrderBy(q => q.Metadata.Name)
            .Select(q =>
            {
                var queryName =
                    q.Metadata.Name.ToLowerInvariant();

                var recordName =
                    registration.Metadata.Name.ToLowerInvariant();

                return new QueryableNamedQuerySummary
                {
                    Name = q.Metadata.Name,

                    Description = q.Metadata.Description,

                    ExecuteUrl = QueryableContractUrls.NamedQuery(
                        options,
                        recordName,
                        queryName),

                    MetadataUrl = QueryableContractUrls.NamedQueryMetadata(
                        options,
                        recordName,
                        queryName)
                };
            })
            .ToArray()
        };
    }
    public static QueryableRecordSummary ToSummary(
        RecordRegistration registration,
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var routePrefix =
            options.RoutePrefix.TrimEnd('/');

        var recordName =
            registration.Metadata.Name.ToLowerInvariant();

        var recordBase =
            $"{routePrefix}/{recordName}";

        return new QueryableRecordSummary
        {
            Name = registration.Metadata.Name,

            Description = registration.Metadata.Description,

            MetadataUrl = QueryableContractUrls.RecordMetadata(options, recordName),

            QueryUrl = QueryableContractUrls.RecordQuery(options, recordName),

            NamedQueries = registration.NamedQueryTypes
            .OrderBy(q => q.Metadata.Name)
            .Select(q =>
            {
                var queryName = q.Metadata.Name.ToLowerInvariant();

                return new QueryableNamedQuerySummary
                {
                    Name = q.Metadata.Name,
                    Description = q.Metadata.Description,
                    ExecuteUrl = QueryableContractUrls.NamedQuery(
                        options,
                        recordName,
                        queryName),
                    MetadataUrl = QueryableContractUrls.NamedQueryMetadata(
                        options,
                        recordName,
                        queryName)
                };
            })
            .ToArray()
        };
    }

}
