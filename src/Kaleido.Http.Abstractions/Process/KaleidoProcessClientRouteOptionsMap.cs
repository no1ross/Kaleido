namespace Kaleido.Http.Abstractions.Process;

/// <summary>
/// Holds the route prefix (service name) registered for each named process client.
/// Populated at registration time by the client registration extension.
/// </summary>
public sealed class KaleidoProcessClientRouteOptionsMap
{
    /// <summary>Key = client name, Value = route prefix for that remote service.</summary>
    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
}
