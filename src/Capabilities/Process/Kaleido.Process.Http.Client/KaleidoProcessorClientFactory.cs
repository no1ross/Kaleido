namespace Kaleido.Process.Http.Client;

internal sealed class KaleidoProcessorClientFactory(
    IKaleidoRegistryTargetResolver targets,
    IHttpClientFactory httpClientFactory,
    IKaleidoInboundCallerAccessor inboundCaller,
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoServiceOptions serviceOptions)
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
        return new KaleidoProcessorClient(serviceName, serviceVersion, targets, httpClientFactory, inboundCaller, correlation, serviceOptions);
    }
}
