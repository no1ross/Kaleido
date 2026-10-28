namespace Kaleido.Http.Queryable;

internal static class QueryableRoutePaths
{
    public static string QuerySourceQuery(string sourceName)
        => $"{sourceName}/query";

    public static string QueryViewQuery(string sourceName, string viewName)
        => $"{sourceName}/{viewName}/query";
}
