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
}
