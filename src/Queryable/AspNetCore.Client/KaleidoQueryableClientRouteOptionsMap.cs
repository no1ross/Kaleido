namespace Kaleido.Queryable.AspNetCore.Client;

/// <summary>
/// Holds the <see cref="QueryableRouteOptions"/> registered for each named queryable client.
/// Populated at registration time by <see cref="KaleidoQueryableClientServiceCollectionExtensions.AddQueryableClient"/>.
/// </summary>
internal sealed class KaleidoQueryableClientRouteOptionsMap
{
    public Dictionary<string, QueryableRouteOptions> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
}
