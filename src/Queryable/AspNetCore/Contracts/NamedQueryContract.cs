//using Kaleido.Queryable.Metadata;

//namespace Kaleido.Queryable.AspNetCore;

///// <summary>
///// API-safe named query metadata.
///// </summary>
//public sealed record NamedQueryContract
//{
//    public required string Name { get; init; }

//    public string? Description { get; init; }

//    public static NamedQueryContract FromNamedQuery(RuntimeAllowedQueryMetadata namedQuery)
//    {
//        ArgumentNullException.ThrowIfNull(namedQuery);

//        return new NamedQueryContract
//        {
//            Name = namedQuery.Name,
//            Description = namedQuery.Description
//        };
//    }
//}
