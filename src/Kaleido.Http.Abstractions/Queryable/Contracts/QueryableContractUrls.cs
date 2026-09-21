internal static class QueryableContractUrls
{
    internal static string QueryablePrefix(string serviceName) =>
        string.IsNullOrWhiteSpace(serviceName)
            ? "/queryable"
            : $"/{serviceName.Trim().Trim('/')}/queryable";

    public static string QueryRegistry(string serviceName)
        => $"{QueryablePrefix(serviceName)}/registry";

    public static string QueryContextMetadata(string serviceName, string contextName)
        => $"{QueryablePrefix(serviceName)}/{contextName}/metadata";

    public static string QueryContextQuery(string serviceName, string contextName)
        => $"{QueryablePrefix(serviceName)}/{contextName}/query";

    public static string QueryViewQuery(string serviceName, string contextName, string viewName)
        => $"{QueryablePrefix(serviceName)}/{contextName}/{viewName}/query";

    public static string NamedQuery(string serviceName, string contextName, string viewName, string queryName)
        => $"{QueryablePrefix(serviceName)}/{contextName}/{viewName}/{queryName}";

    public static string NamedQueryMetadata(string serviceName, string contextName, string viewName, string queryName)
        => $"{QueryablePrefix(serviceName)}/{contextName}/{viewName}/{queryName}/metadata";
}
