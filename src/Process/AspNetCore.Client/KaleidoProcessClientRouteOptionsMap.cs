namespace Kaleido.Process.AspNetCore.Client;

/// <summary>
/// Holds the <see cref="ProcessRouteOptions"/> registered for each named process client.
/// Populated at registration time by <see cref="KaleidoProcessClientServiceCollectionExtensions.AddProcessClient"/>.
/// </summary>
internal sealed class KaleidoProcessClientRouteOptionsMap
{
    public Dictionary<string, ProcessRouteOptions> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
}
