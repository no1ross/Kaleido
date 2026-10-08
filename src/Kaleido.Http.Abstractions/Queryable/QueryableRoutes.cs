namespace Kaleido.Http.Queryable;

#pragma warning disable KAL0001 // Pure route/endpoint wiring — no state, intentional static

internal static class QueryableContractUrls
{
    internal static string QueryablePrefix(string serviceName) =>
        string.IsNullOrWhiteSpace(serviceName)
            ? "/queryable"
            : $"/{serviceName.Trim().Trim('/')}/queryable";

    public static string QuerySourceQuery(string serviceName, string sourceName)
        => $"{QueryablePrefix(serviceName)}/{sourceName}/query";

    public static string QueryViewQuery(string serviceName, string sourceName, string viewName)
        => $"{QueryablePrefix(serviceName)}/{sourceName}/{viewName}/query";
}
