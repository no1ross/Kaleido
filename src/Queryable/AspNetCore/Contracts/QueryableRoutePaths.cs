using Kaleido.Queryable.AspNetCore;

internal static class QueryableRoutePaths
{
    public static string RecordMetadata(
        QueryableAspNetCoreOptions options,
        string recordName)
        => $"{recordName}/{options.MetadataRoute}";

    public static string RecordQuery(
        QueryableAspNetCoreOptions options,
        string recordName)
        => $"{recordName}/{options.QueryRoute}";

    public static string NamedQuery(
        QueryableAspNetCoreOptions options,
        string recordName,
        string queryName)
        => $"{recordName}/{options.QueriesRoute}/{queryName}";

    public static string NamedQueryMetadata(
        QueryableAspNetCoreOptions options,
        string recordName,
        string queryName)
        => $"{recordName}/{options.QueriesRoute}/{queryName}/{options.MetadataRoute}";
}
