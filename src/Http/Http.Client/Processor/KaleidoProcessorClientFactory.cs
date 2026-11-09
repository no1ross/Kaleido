namespace Kaleido.Http.Client.Processor;

internal sealed class KaleidoProcessorClientFactory(
    IKaleidoRegistryTargetResolver targets,
    IKaleidoClientTransport transport)
    : IKaleidoProcessorClientFactory
{
    public IKaleidoProcessorClient GetClient(string serviceName) => CreateClient(serviceName, serviceVersion: null);

    public IKaleidoProcessorClient GetClient(string serviceName, string serviceVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        return CreateClient(serviceName, serviceVersion);
    }

    private KaleidoProcessorClient CreateClient(string serviceName, string? serviceVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        return new KaleidoProcessorClient(serviceName, serviceVersion, targets, transport);
    }
}
