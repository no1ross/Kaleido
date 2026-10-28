using Kaleido.Exceptions;

namespace Kaleido.Http.Client;

internal static class KaleidoClientCatalogExtensions
{
    /// <summary>
    /// The client configured under <paramref name="name"/> (case-insensitive); a name not configured
    /// under <c>Kaleido:Clients</c> is a configuration error naming the clients that are.
    /// </summary>
    public static KaleidoClientDefinition Require(this IKaleidoClientCatalog catalog, string name) =>
        catalog.Find(name)
        ?? throw new KaleidoConfigurationException(
            HttpClientErrorCodes.UnknownClient,
            $"Kaleido client '{name}' is not configured under Kaleido:Clients; {Configured(catalog)}.");

    private static string Configured(IKaleidoClientCatalog catalog) =>
        catalog.Clients.Count == 0
            ? "no clients are configured"
            : "configured clients: " + string.Join(", ", catalog.Clients.Select(x => x.Name).Order(StringComparer.OrdinalIgnoreCase));
}
