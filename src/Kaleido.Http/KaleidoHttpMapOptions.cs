namespace Kaleido.Http;

/// <summary>
/// Options for <see cref="KaleidoEndpointRouteBuilderExtensions.MapKaleidoHttp"/>.
/// </summary>
public sealed class KaleidoHttpMapOptions
{
    /// <summary>
    /// When true, the registry endpoint aggregates downstream services:
    /// every client registered via <c>AddHttpClients()</c> is fetched and
    /// merged into this service's response. Requires <c>AddHttpClients()</c>.
    /// When false (default), <c>GET /{service}/registry</c> returns only this
    /// service's local registrations.
    /// </summary>
    public bool AggregateRegistry { get; set; }
}
