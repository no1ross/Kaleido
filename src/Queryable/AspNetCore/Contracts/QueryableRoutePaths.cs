using Kaleido.Queryable.AspNetCore;

internal static class QueryableRoutePaths
{
    public static string RecordMetadata(
        QueryableRouteOptions options,
        string recordName)
        => $"{recordName}/{options.MetadataRoute}";

    public static string RecordQuery(
        QueryableRouteOptions options,
        string recordName)
        => $"{recordName}/{options.QueryRoute}";

    public static string NamedQuery(
        QueryableRouteOptions options,
        string recordName,
        string queryName)
        => $"{recordName}/{options.QueriesRoute}/{queryName}";

    public static string NamedQueryMetadata(
        QueryableRouteOptions options,
        string recordName,
        string queryName)
        => $"{recordName}/{options.QueriesRoute}/{queryName}/{options.MetadataRoute}";
}
