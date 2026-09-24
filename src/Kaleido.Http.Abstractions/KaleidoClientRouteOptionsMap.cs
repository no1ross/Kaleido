namespace Kaleido.Http;

/// <summary>
/// Contract for the DI-singleton that holds the name-to-routePrefix map for
/// a named Kaleido HTTP client. Used as a type constraint on TMap in
/// KaleidoClientFactoryBase and KaleidoClientExtensions — eliminates reflection
/// over the Options property. IDictionary is intentional: the registration
/// extension writes into this map at startup; consumers read from it at request time.
/// </summary>
internal interface IKaleidoClientRouteOptionsMap
{
    // KAL0018: IDictionary is intentional — KaleidoClientExtensions writes
    // client name → route prefix entries at startup registration time.
#pragma warning disable KAL0018
    IDictionary<string, string> Options { get; }
#pragma warning restore KAL0018
}

/// <summary>
/// Base implementation of <see cref="IKaleidoClientRouteOptionsMap"/>.
/// Subclassed once per client type so each registers as a distinct DI singleton
/// without key collision.
/// </summary>
internal abstract class KaleidoClientRouteOptionsMap : IKaleidoClientRouteOptionsMap
{
    // KAL0018: see IKaleidoClientRouteOptionsMap.Options
#pragma warning disable KAL0018
    public IDictionary<string, string> Options { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
#pragma warning restore KAL0018
}
