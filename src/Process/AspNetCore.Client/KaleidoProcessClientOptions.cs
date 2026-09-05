namespace Kaleido.Process.AspNetCore.Client;

/// <summary>
/// Registration options for a named Kaleido process client.
/// </summary>
public sealed class KaleidoProcessClientOptions
{
    /// <summary>
    /// The name used to identify this client in <see cref="IKaleidoProcessClientFactory"/>.
    /// Also used as the named <see cref="System.Net.Http.IHttpClientFactory"/> key.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The base URL of the remote Kaleido process server.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The route prefix used by the remote server (e.g. <c>"kaleido"</c>).
    /// Defaults to an empty string.
    /// </summary>
    public string RoutePrefix { get; set; } = string.Empty;
}
