using Kaleido.Queryable;

internal static class QueryableContractUrls
{
    public static string QueryablePrefix(
        QueryableRouteOptions options)
        => string.IsNullOrWhiteSpace(options.RoutePrefix)
            ? "/queryable"
            : $"/{options.RoutePrefix.Trim().Trim('/')}/queryable";

    public static string QueryContextMetadata(
        QueryableRouteOptions options,
        string contextName)
        => $"{QueryablePrefix(options)}/{contextName}/{options.MetadataRoute}";

    public static string QueryViewQuery(
        QueryableRouteOptions options,
        string contextName,
        string viewName)
        => $"{QueryablePrefix(options)}/{contextName}/{viewName}/{options.QueryRoute}";

    public static string NamedQuery(
        QueryableRouteOptions options,
        string contextName,
        string viewName,
        string queryName)
        => $"{QueryablePrefix(options)}/{contextName}/{viewName}/{queryName}";

    public static string NamedQueryMetadata(
        QueryableRouteOptions options,
        string contextName,
        string viewName,
        string queryName)
        => $"{QueryablePrefix(options)}/{contextName}/{viewName}/{queryName}/{options.MetadataRoute}";
}