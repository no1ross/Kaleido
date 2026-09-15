namespace Kaleido.Process.AspNetCore.Client;

/// <summary>
/// Holds the route prefix (service name) registered for each named process client.
/// Populated at registration time by <see cref="KaleidoProcessClientServiceCollectionExtensions.AddProcessClient"/>.
/// </summary>
internal sealed class KaleidoProcessClientRouteOptionsMap
{
    /// <summary>Key = client name, Value = route prefix for that remote service.</summary>
    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
}
