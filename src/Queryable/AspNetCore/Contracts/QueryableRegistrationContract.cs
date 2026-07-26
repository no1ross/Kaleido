//using Kaleido.Queryable.Metadata;

//namespace Kaleido.Queryable.AspNetCore;

///// <summary>
///// API-safe contract that describes a registered queryable record set.
///// </summary>
//public sealed record RecordRegistrationContract
//{
//    public required string Name { get; init; }

//    public string? Description { get; init; }

//    public required string RecordType { get; init; }

//    public IReadOnlyCollection<QueryableFieldContract> Fields { get; init; }
//        = Array.Empty<QueryableFieldContract>();

//    public IReadOnlyCollection<NamedQueryContract> NamedQueries { get; init; }
//        = Array.Empty<NamedQueryContract>();

//    public static RecordRegistrationContract FromRegistration(RecordRegistration registration)
//    {
//        ArgumentNullException.ThrowIfNull(registration);

//        return new RecordRegistrationContract
//        {
//            Name = registration.Metadata.Name,
//            Description = registration.Metadata.Description,
//            RecordType = registration.RecordType.Name,
//            Fields = registration.Metadata.Fields
//                .Select(QueryableFieldContract.FromField)
//                .ToArray(),
//            NamedQueries = registration.NamedQueryTypes
//                .Select(NamedQueryContract.FromNamedQuery)
//                .ToArray()
//        };
//    }
//}
