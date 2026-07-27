using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts
{
    public sealed record RecordContract
    {
        public required string Name { get; init; }

        public string? Description { get; init; }

        public string? Version { get; init; }

        public string? Source { get; init; }

        public PageableContract? Pageable { get; init; }

        public IReadOnlyCollection<FieldContract> Fields { get; init; }
            = Array.Empty<FieldContract>();

        public IReadOnlyCollection<NamedQuerySummaryContract> NamedQueries { get; init; }
            = Array.Empty<NamedQuerySummaryContract>();

        public static RecordContract FromRegistration(
            RecordRegistration registration,
            QueryableAspNetCoreOptions options)
        {
            ArgumentNullException.ThrowIfNull(registration);

            return new RecordContract
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
                    .Select(FieldContract.FromMetadata)
                    .ToArray(),
                NamedQueries = registration.NamedQueryTypes
                .OrderBy(q => q.Metadata.Name)
                .Select(q =>
                {
                    var queryName =
                        q.Metadata.Name.ToLowerInvariant();

                    var recordName =
                        registration.Metadata.Name.ToLowerInvariant();

                    return new NamedQuerySummaryContract
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
        public static RecordSummaryContract ToSummary(
            RecordRegistration registration,
            QueryableAspNetCoreOptions options)
        {
            ArgumentNullException.ThrowIfNull(registration);
            ArgumentNullException.ThrowIfNull(options);

            var routePrefix =
                options.RoutePrefix.TrimEnd('/');

            var recordName =
                registration.Metadata.Name.ToLowerInvariant();

            var recordBase =
                $"{routePrefix}/{recordName}";

            return new RecordSummaryContract
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

                    return new NamedQuerySummaryContract
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
}
