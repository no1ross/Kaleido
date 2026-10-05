namespace Kaleido.Http.Queryable;

#pragma warning disable KAL0001 // Pure name factory — no state, intentional static
public static class QueryableEndpointNames
{
    public static string QueryContextEndpointName(
        string contextName)
        => $"KaleidoQueryableQuery_{contextName}";

    public static string QueryViewEndpointName(
        string contextName,
        string viewName)
        => $"KaleidoQueryableViewQuery_{contextName}_{viewName}";

}