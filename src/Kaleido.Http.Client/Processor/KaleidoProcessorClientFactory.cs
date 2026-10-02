using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Processor;

internal sealed class KaleidoProcessorClientFactory(
    IHttpClientFactory httpClientFactory,
    ICorrelationHeaderStamper headerStamper,
    ILogger<KaleidoProcessorClient> logger,
    KaleidoProcessorClientRouteOptionsMap routeOptionsMap,
    KaleidoRemoteRegistry remoteRegistry)
    : KaleidoClientFactoryBase<IKaleidoProcessorClient, KaleidoProcessorClientRouteOptionsMap>,
      IKaleidoProcessorClientFactory
{
    protected override IHttpClientFactory HttpClientFactory => httpClientFactory;
    protected override ICorrelationHeaderStamper HeaderStamper => headerStamper;
    protected override KaleidoProcessorClientRouteOptionsMap RouteOptionsMap => routeOptionsMap;

    protected override IKaleidoProcessorClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        ICorrelationHeaderStamper stamper,
        string clientName,
        string serviceName,
        TimeSpan? registryTtl)
    {
        return new KaleidoProcessorClient(httpClient, stamper, logger, remoteRegistry, clientName, serviceName, registryTtl);
    }
}
