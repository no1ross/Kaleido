using Kaleido.Queryable.AspNetCore;

internal static class QueryableContractUrls
{
    public static string RecordMetadata(
        QueryableAspNetCoreOptions options,
        string recordName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.MetadataRoute}";

    public static string RecordQuery(
        QueryableAspNetCoreOptions options,
        string recordName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.QueryRoute}";

    public static string NamedQuery(
        QueryableAspNetCoreOptions options,
        string recordName,
        string queryName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.QueriesRoute}/{queryName}";

    public static string NamedQueryMetadata(
        QueryableAspNetCoreOptions options,
        string recordName,
        string queryName)
        => $"{NamedQuery(options, recordName, queryName)}/{options.MetadataRoute}";
}