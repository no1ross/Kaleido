namespace Kaleido.Queryable.Http;

/// <summary>Builds versioned logical HTTP routes for a Queryable source or view.</summary>
/// <remarks>Servers publish these paths; outbound clients resolve the published route from the
/// central registry instead of constructing a destination from a prefix.</remarks>
public static class QueryableContractUrls
{
    /// <summary>Builds the prefix beneath which this service's Queryable endpoints are mapped.</summary>
    /// <param name="serviceName">The service publishing the sources.</param>
    /// <param name="serviceVersion">The required service version.</param>
    /// <returns>The relative <c>/{service}/{version}/queryable</c> route.</returns>
    internal static string QueryablePrefix(string serviceName, string serviceVersion) =>
        $"{KaleidoHttpRoutePrefix.Service(serviceName, serviceVersion)}/queryable";

    /// <summary>Builds the logical query URL for a source's direct query.</summary>
    /// <param name="serviceName">The service publishing the source.</param>
    /// <param name="serviceVersion">The required version of that service.</param>
    /// <param name="sourceName">The source's chosen route segment, normally its lowercase type name.</param>
    /// <returns>The versioned source query path ending in <c>/query</c>.</returns>
    public static string QuerySourceQuery(string serviceName, string serviceVersion, string sourceName) =>
        $"{QueryablePrefix(serviceName, serviceVersion)}/{sourceName}/query";

    /// <summary>Builds the logical query URL for a view of a source.</summary>
    /// <param name="serviceName">The service publishing the source and view.</param>
    /// <param name="serviceVersion">The required version of that service.</param>
    /// <param name="sourceName">The source's chosen route segment, normally its lowercase type name.</param>
    /// <param name="viewName">The view's chosen route segment, normally its lowercase type name.</param>
    /// <returns>The versioned source/view query path ending in <c>/query</c>.</returns>
    public static string QueryViewQuery(string serviceName, string serviceVersion, string sourceName, string viewName) =>
        $"{QueryablePrefix(serviceName, serviceVersion)}/{sourceName}/{viewName}/query";

}
