namespace Kaleido.Http.Process;

/// <summary>
/// Holds the route prefix (service name) registered for each named process client.
/// Populated at registration time by the client registration extension.
/// </summary>
public sealed class KaleidoProcessClientRouteOptionsMap
{
    // KAL0018: IDictionary is required — KaleidoClientExtensions resolves this
    // property via reflection and casts to IDictionary<string, string> to write
    // client route entries into it during startup registration.
#pragma warning disable KAL0018
    public IDictionary<string, string> Options { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
#pragma warning restore KAL0018
}
