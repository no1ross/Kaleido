namespace Kaleido.Http.Queryable;

#pragma warning disable KAL0001 // Pure route factory — no state, intentional static
public static class QueryableRoutePaths
{
    public static string QuerySourceQuery(string sourceName)
        => $"{sourceName}/query";

    public static string QueryViewQuery(string sourceName, string viewName)
        => $"{sourceName}/{viewName}/query";
}
