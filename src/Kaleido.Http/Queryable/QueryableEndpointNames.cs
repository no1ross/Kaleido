namespace Kaleido.Http.Queryable;

internal static class QueryableEndpointNames
{
    public static string QuerySourceEndpointName(
        string sourceName)
        => $"KaleidoQueryableQuery_{sourceName}";

    public static string QueryViewEndpointName(
        string sourceName,
        string viewName)
        => $"KaleidoQueryableViewQuery_{sourceName}_{viewName}";

}
