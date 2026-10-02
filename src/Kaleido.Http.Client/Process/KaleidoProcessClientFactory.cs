using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Process;

internal sealed class KaleidoProcessClientFactory(
    IHttpClientFactory httpClientFactory,
    ICorrelationHeaderStamper headerStamper,
    ILogger<KaleidoProcessClient> logger,
    KaleidoProcessClientRouteOptionsMap routeOptionsMap,
    KaleidoRemoteRegistry remoteRegistry)
    : KaleidoClientFactoryBase<IKaleidoProcessClient, KaleidoProcessClientRouteOptionsMap>,
      IKaleidoProcessClientFactory
{
    protected override IHttpClientFactory HttpClientFactory => httpClientFactory;
    protected override ICorrelationHeaderStamper HeaderStamper => headerStamper;
    protected override KaleidoProcessClientRouteOptionsMap RouteOptionsMap => routeOptionsMap;

    protected override IKaleidoProcessClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        ICorrelationHeaderStamper stamper,
        string clientName,
        string serviceName,
        TimeSpan? registryTtl)
    {
        return new KaleidoProcessClient(httpClient, stamper, logger, remoteRegistry, clientName, serviceName, registryTtl);
    }
}
