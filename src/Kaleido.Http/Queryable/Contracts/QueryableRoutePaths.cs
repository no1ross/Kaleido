namespace Kaleido.Http.Queryable;

public static class QueryableRoutePaths
{
    public static string QueryContextMetadata(string contextName)
        => $"{contextName}/metadata";

    public static string QueryContextQuery(string contextName)
        => $"{contextName}/query";

    public static string QueryViewQuery(string contextName, string viewName)
        => $"{contextName}/{viewName}/query";
}
