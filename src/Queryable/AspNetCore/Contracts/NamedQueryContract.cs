using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record NamedQueryContract
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyCollection<QueryParameterContract> Parameters { get; init; }
        = Array.Empty<QueryParameterContract>();

    public static NamedQueryContract FromRegistration(
        NamedQueryRegistration registration)
    {
        return new NamedQueryContract
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            Parameters = registration.Metadata.Parameters?
                .Select(QueryParameterContract.FromMetadata)
                .ToArray()
                ?? Array.Empty<QueryParameterContract>()
        };
    }
}
