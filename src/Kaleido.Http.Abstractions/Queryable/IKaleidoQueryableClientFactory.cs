namespace Kaleido.Http.Queryable;

public interface IKaleidoQueryableClientFactory
{
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
