namespace Kaleido.Queryable.AspNetCore.Client;

/// <summary>
/// Registration options for a named Kaleido queryable client.
/// </summary>
public sealed class KaleidoQueryableClientOptions
{
    /// <summary>
    /// The name used to identify this client in the queryable client factory.
    /// Also used as the named <see cref="System.Net.Http.IHttpClientFactory"/> key.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The base URL of the remote Kaleido queryable server.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The route prefix used by the remote server (e.g. <c>"kaleido"</c>).
    /// Defaults to an empty string.
    /// </summary>
    public string RoutePrefix { get; set; } = string.Empty;
}
