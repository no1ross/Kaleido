namespace Kaleido.Http.Client.Configuration;

/// <summary>
/// Registration options for a named Kaleido HTTP client.
/// </summary>
internal sealed record KaleidoHttpClientOptions
{
    /// <summary>
    /// The name used to identify this client in the client factory.
    /// Also used as the named <see cref="IHttpClientFactory"/> key.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The base URL of the remote Kaleido server.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// The route prefix used by the remote server (e.g. <c>"kaleido"</c>).
    /// Defaults to an empty string.
    /// </summary>
    public string RoutePrefix { get; init; } = string.Empty;

    /// <summary>The client's resilience (null = none). <c>AddHttpClients()</c> sets it from the shared and the client's section.</summary>
    public KaleidoResilienceOptions? Resilience { get; init; }
}
