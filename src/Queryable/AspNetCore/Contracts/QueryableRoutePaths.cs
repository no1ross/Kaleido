using Kaleido.Queryable;

public static class QueryableRoutePaths
{
    public static string QueryContextMetadata(
        QueryableRouteOptions options,
        string contextName)
        => $"{contextName}/{options.MetadataRoute}";

    public static string QueryContextQuery(
        QueryableRouteOptions options,
        string contextName)
        => $"{contextName}/{options.QueryRoute}";

    public static string QueryViewQuery(
        QueryableRouteOptions options,
        string contextName,
        string viewName)
        => $"{contextName}/{viewName}/{options.QueryRoute}";
}
