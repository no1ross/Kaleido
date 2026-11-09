namespace Kaleido.Http.Queryable;

internal static class QueryableContractUrls
{
    internal static string QueryablePrefix(string serviceName, string serviceVersion) =>
        $"{KaleidoHttpRoutePrefix.Service(serviceName, serviceVersion)}/queryable";

    public static string QuerySourceQuery(string serviceName, string serviceVersion, string sourceName) =>
        $"{QueryablePrefix(serviceName, serviceVersion)}/{sourceName}/query";

    public static string QueryViewQuery(string serviceName, string serviceVersion, string sourceName, string viewName) =>
        $"{QueryablePrefix(serviceName, serviceVersion)}/{sourceName}/{viewName}/query";

}
