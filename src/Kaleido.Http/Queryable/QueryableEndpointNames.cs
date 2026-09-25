namespace Kaleido.Http.Queryable;

#pragma warning disable KAL0001 // Pure name factory — no state, intentional static
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

}