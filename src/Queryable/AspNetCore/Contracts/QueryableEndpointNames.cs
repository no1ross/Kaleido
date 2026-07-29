namespace Kaleido.Queryable.AspNetCore.Contracts;

public static class QueryableEndpointNames
{
    public static string CatalogEndpointName =>
        "queryable-catalog";

    public static string RecordMetadataEndpointName(
        string recordName)
        => $"{recordName}-metadata";

    public static string RecordQueryEndpointName(
        string recordName)
        => $"{recordName}-query";

    public static string NamedQueryEndpointName(
        string recordName,
        string queryName)
        => $"{recordName}-{queryName}";

    public static string NamedQueryMetadataEndpointName(
        string recordName,
        string queryName)
        => $"{recordName}-{queryName}-metadata";
}
