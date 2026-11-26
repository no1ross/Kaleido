namespace Kaleido.Queryable.Http.Client;

internal sealed class KaleidoQueryableClientFactory(
    IKaleidoRegistryTargetResolver targets,
    IHttpClientFactory httpClientFactory,
    IKaleidoInboundCallerAccessor inboundCaller,
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoServiceOptions localServiceOptions)
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
        return new KaleidoQueryableClient(serviceName, serviceVersion, targets, httpClientFactory, inboundCaller, correlation, localServiceOptions);
    }
}
