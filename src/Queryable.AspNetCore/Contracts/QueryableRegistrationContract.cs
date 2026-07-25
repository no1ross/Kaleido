using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// API-safe contract that describes a registered queryable record set.
/// </summary>
public sealed record QueryableRegistrationContract
{
    public required string Key { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public required string RecordType { get; init; }

    public IReadOnlyCollection<QueryableFieldContract> Fields { get; init; }
        = Array.Empty<QueryableFieldContract>();

    public IReadOnlyCollection<NamedQueryContract> NamedQueries { get; init; }
        = Array.Empty<NamedQueryContract>();

    public static QueryableRegistrationContract FromRegistration(RecordRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new QueryableRegistrationContract
        {
            Key = registration.RuntimeMetadata.Key,
            Name = registration.RecordType.Name,
            Description = registration.RuntimeMetadata.Description,
            RecordType = registration.RecordType.Name,
            Fields = registration.RuntimeMetadata.Fields
                .Select(QueryableFieldContract.FromField)
                .ToArray(),
            NamedQueries = registration.NamedQueries
                .Select(NamedQueryContract.FromNamedQuery)
                .ToArray()
        };
    }
}
