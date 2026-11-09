namespace Kaleido.Http.Client.Queryable;

internal sealed class KaleidoQueryableClientFactory(
    IKaleidoRegistryTargetResolver targets,
    IKaleidoClientTransport transport)
    : IKaleidoQueryableClientFactory
{
    public IKaleidoQueryableClient GetClient(string serviceName) => CreateClient(serviceName, serviceVersion: null);

    public IKaleidoQueryableClient GetClient(string serviceName, string serviceVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        return CreateClient(serviceName, serviceVersion);
    }

    private KaleidoQueryableClient CreateClient(string serviceName, string? serviceVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        return new KaleidoQueryableClient(serviceName, serviceVersion, targets, transport);
    }
}
