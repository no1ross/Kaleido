namespace Kaleido.Queryable.Http.Client;

internal static class QueryablePublishedHttpRoute
{
    internal static string RequirePath(string? path, string gatewayRoute)
    {
        if (path is null || !SafePath(gatewayRoute) || !SafePath(path)
            || !path.StartsWith(gatewayRoute + "/", StringComparison.Ordinal))
        {
            throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                "The central registry did not publish a safe relative HTTP route for this service version.");
        }

        return path;
    }

    private static bool SafePath(string? path) =>
        path is { Length: > 1 }
        && path[0] == '/' && path[1] != '/'
        && path.IndexOfAny(['%', '\\', '?', '#']) < 0
        && path.Split('/').Skip(1).All(segment => segment.Length > 0 && segment is not ("." or ".."));
}
