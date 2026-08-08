using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableNamedQuery
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyCollection<QueryableQueryParameter> Parameters { get; init; }
        = Array.Empty<QueryableQueryParameter>();

    public static QueryableNamedQuery FromRegistration(
        NamedQueryRegistration registration)
    {
        return new QueryableNamedQuery
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            Parameters = registration.Metadata.Parameters?
                .Select(QueryableQueryParameter.FromMetadata)
                .ToArray()
                ?? Array.Empty<QueryableQueryParameter>()
        };
    }
}
