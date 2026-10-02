using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Queryable;

internal sealed class KaleidoQueryableClientFactory(
    IHttpClientFactory httpClientFactory,
    ICorrelationHeaderStamper headerStamper,
    ILogger<KaleidoQueryableClient> logger,
    KaleidoQueryableClientRouteOptionsMap routeOptionsMap,
    KaleidoRemoteRegistry remoteRegistry)
    : KaleidoClientFactoryBase<IKaleidoQueryableClient, KaleidoQueryableClientRouteOptionsMap>,
      IKaleidoQueryableClientFactory
{
    protected override IHttpClientFactory HttpClientFactory => httpClientFactory;
    protected override ICorrelationHeaderStamper HeaderStamper => headerStamper;
    protected override KaleidoQueryableClientRouteOptionsMap RouteOptionsMap => routeOptionsMap;

    protected override IKaleidoQueryableClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        ICorrelationHeaderStamper stamper,
        string clientName,
        string serviceName,
        TimeSpan? registryTtl)
    {
        return new KaleidoQueryableClient(httpClient, stamper, logger, remoteRegistry, clientName, serviceName, registryTtl);
    }
}
