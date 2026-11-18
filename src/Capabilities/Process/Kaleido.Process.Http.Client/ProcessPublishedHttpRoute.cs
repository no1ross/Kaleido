namespace Kaleido.Process.Http.Client;

internal static class ProcessPublishedHttpRoute
{
    internal static string RequirePath(string? path, string gatewayRoute)
    {
        if (path is null || !SafePath(gatewayRoute) || !SafePath(path)
            || !path.StartsWith(gatewayRoute + "/", StringComparison.Ordinal))
        {
            throw Unavailable();
        }

        return path;
    }

    internal static string ProcessState(string? template, Guid processId, string gatewayRoute)
    {
        var safe = RequirePath(template, gatewayRoute);
        const string token = "{processId}";
        if (!string.Equals(safe.Split('/').LastOrDefault(), token, StringComparison.Ordinal)
            || safe.IndexOf(token, StringComparison.Ordinal) != safe.LastIndexOf(token, StringComparison.Ordinal))
        {
            throw Unavailable();
        }

        return RequirePath(safe.Replace(token, processId.ToString("D"), StringComparison.Ordinal), gatewayRoute);
    }

    private static bool SafePath(string? path) =>
        path is { Length: > 1 }
        && path[0] == '/' && path[1] != '/'
        && path.IndexOfAny(['%', '\\', '?', '#']) < 0
        && path.Split('/').Skip(1).All(segment => segment.Length > 0 && segment is not ("." or ".."));

    private static KaleidoRegistryClientException Unavailable() =>
        new(RegistryClientErrorCodes.RouteUnavailable,
            "The hydrated Registry did not publish a safe relative Process HTTP route for this service version.");
}
