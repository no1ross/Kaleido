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
    /// How often this client checks whether the remote registry changed: the known revision is sent
    /// as an ETag and an unchanged registry answers 304. When null the registry is fetched once and
    /// kept until it is invalidated (a 404 from the service, or <c>InvalidateRegistryAsync()</c>).
    /// <c>AddHttpClients()</c> sets it from <see cref="KaleidoClientOptions.RegistryTtl"/> (default
    /// 30 seconds).
    /// </summary>
    public TimeSpan? RegistryTtl { get; set; }

    /// <summary>
    /// When true, the <c>kaleido-{name}</c> health check probes
    /// <c>/{prefix}/registry?strict</c> — an aggregating downstream that is
    /// partially degraded reports <c>Unhealthy</c> instead of silently
    /// healthy. Defaults to false.
    /// </summary>
    public bool StrictRegistryProbe { get; set; }

    /// <summary>The client's resilience (null = none). <c>AddHttpClients()</c> sets it from the shared and the client's section.</summary>
    public KaleidoResilienceOptions? Resilience { get; set; }
}
