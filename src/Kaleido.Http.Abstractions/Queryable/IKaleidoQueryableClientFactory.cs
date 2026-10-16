namespace Kaleido.Http.Queryable;

/// <summary>
/// Resolves <see cref="IKaleidoQueryableClient"/> instances for the downstream services configured
/// under <c>Kaleido:Clients</c>; registered by <c>AddHttpClients()</c>.
/// </summary>
public interface IKaleidoQueryableClientFactory
{
    /// <summary>
    /// The client for the downstream service configured under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The client's configured name (its <c>Kaleido:Clients</c> key), matched case-insensitively.</param>
    /// <returns>A client bound to that service's base address and route prefix.</returns>
    /// <remarks>
    /// A name that is not configured fails at the call with a <c>KaleidoConfigurationException</c>
    /// (<c>httpclient_unknown_client</c>) listing the configured clients.
    /// Use <see cref="TryGetClientForService"/> when starting from a registry entry.
    /// </remarks>
    IKaleidoQueryableClient GetClient(string name);

    /// <summary>
    /// The client for the downstream service named <paramref name="serviceName"/> (its route
    /// prefix, as published in the registry's <c>serviceName</c>), when one is configured. Callers
    /// that start from the registry (gateways, AI-tool adapters) know services, not client names.
    /// </summary>
    bool TryGetClientForService(
        string serviceName,
        [NotNullWhen(true)] out IKaleidoQueryableClient? client);
}
