using Kaleido.Queryable.AspNetCore;

internal static class QueryableContractUrls
{
    public static string RecordMetadata(
        QueryableRouteOptions options,
        string recordName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.MetadataRoute}";

    public static string RecordQuery(
        QueryableRouteOptions options,
        string recordName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.QueryRoute}";

    public static string NamedQuery(
        QueryableRouteOptions options,
        string recordName,
        string queryName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.QueriesRoute}/{queryName}";

    public static string NamedQueryMetadata(
        QueryableRouteOptions options,
        string recordName,
        string queryName)
        => $"{NamedQuery(options, recordName, queryName)}/{options.MetadataRoute}";
}