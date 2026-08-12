using Kaleido.Queryable;

internal static class QueryableContractUrls
{
    public static string QueryContextMetadata(
        QueryableRouteOptions options,
        string contextName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{contextName}/{options.MetadataRoute}";

    public static string QueryViewQuery(
        QueryableRouteOptions options,
        string contextName,
        string viewName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{contextName}/{viewName}/{options.QueryRoute}";

    public static string NamedQuery(
        QueryableRouteOptions options,
        string contextName,
        string viewName,
        string queryName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{contextName}/{viewName}/{queryName}";

    public static string NamedQueryMetadata(
        QueryableRouteOptions options,
        string contextName,
        string viewName,
        string queryName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{contextName}/{viewName}/{queryName}/{options.MetadataRoute}";
}