public static class QueryableEndpointNames
{
    public static string CatalogEndpointName =>
        "queryable-catalog";

    public static string RegistryEndpointName =>
        "queryable-registry";

    public static string QueryContextMetadataEndpointName(
        string contextName)
        => $"{contextName}-metadata";

    public static string QueryContextEndpointName(
        string contextName)
        => $"{contextName}-query";

    public static string QueryViewEndpointName(
        string contextName,
        string viewName)
        => $"{contextName}-{viewName}-query";

    public static string NamedQueryEndpointName(
        string contextName,
        string viewName,
        string queryName)
        => $"{contextName}-{viewName}-{queryName}";

    public static string NamedQueryMetadataEndpointName(
        string contextName,
        string viewName,
        string queryName)
        => $"{contextName}-{viewName}-{queryName}-metadata";
}