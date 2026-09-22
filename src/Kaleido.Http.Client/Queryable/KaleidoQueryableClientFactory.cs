namespace Kaleido.Http.Client.Queryable;

internal sealed class KaleidoQueryableClientFactory(
    IHttpClientFactory httpClientFactory,
    IKaleidoCorrelationContextAccessor correlation,
    ICorrelationHeaderStamper headerStamper,
    KaleidoQueryableClientRouteOptionsMap routeOptionsMap)
    : KaleidoClientFactoryBase<IKaleidoQueryableClient, KaleidoQueryableClientRouteOptionsMap>,
      IKaleidoQueryableClientFactory
{
    protected override IHttpClientFactory HttpClientFactory => httpClientFactory;
    protected override IKaleidoCorrelationContextAccessor CorrelationAccessor => correlation;
    protected override ICorrelationHeaderStamper HeaderStamper => headerStamper;
    protected override KaleidoQueryableClientRouteOptionsMap RouteOptionsMap => routeOptionsMap;

    protected override IKaleidoQueryableClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        ICorrelationHeaderStamper stamper,
        string serviceName)
    {
        return new KaleidoQueryableClient(httpClient, stamper, serviceName);
    }
}
