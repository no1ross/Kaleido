namespace Kaleido.Http.Queryable;

#pragma warning disable KAL0001 // Pure name factory — no state, intentional static
public static class QueryableEndpointNames
{
    public static string QuerySourceEndpointName(
        string sourceName)
        => $"KaleidoQueryableQuery_{sourceName}";

    public static string QueryViewEndpointName(
        string sourceName,
        string viewName)
        => $"KaleidoQueryableViewQuery_{sourceName}_{viewName}";

}
