namespace Kaleido.Http.Client;

/// <summary>
/// Registration options for a named Kaleido HTTP client.
/// </summary>
public sealed class KaleidoHttpClientOptions
{
    /// <summary>
    /// The name used to identify this client in the client factory.
    /// Also used as the named <see cref="IHttpClientFactory"/> key.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The base URL of the remote Kaleido server.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The route prefix used by the remote server (e.g. <c>"kaleido"</c>).
    /// Defaults to an empty string.
    /// </summary>
    public string RoutePrefix { get; set; } = string.Empty;

    /// <summary>
    /// Maximum age of this client's cached registry snapshot. When null
    /// (default) the registry is fetched once and held for the lifetime of the
    /// process unless <c>InvalidateRegistry()</c> is called. Set this on
    /// long-running consumers so downstream redeploys become visible without a
    /// restart.
    /// </summary>
    public TimeSpan? RegistryTtl { get; set; }

    /// <summary>
    /// When true, the <c>kaleido-{name}</c> health check probes
    /// <c>/{prefix}/registry?strict</c> — an aggregating downstream that is
    /// partially degraded reports <c>Unhealthy</c> instead of silently
    /// healthy. Defaults to false.
    /// </summary>
    public bool StrictRegistryProbe { get; set; }
}
