namespace Kaleido.Http.Queryable.Contracts;

#pragma warning disable KAL0001 // Pure route factory — no state, intentional static
public static class QueryableRoutePaths
{
    public static string QueryContextMetadata(string contextName)
        => $"{contextName}/metadata";

    public static string QueryContextQuery(string contextName)
        => $"{contextName}/query";

    public static string QueryViewQuery(string contextName, string viewName)
        => $"{contextName}/{viewName}/query";
}
