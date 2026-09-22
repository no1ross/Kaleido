namespace Kaleido.Http.Client.Process;

internal sealed class KaleidoProcessClientFactory(
    IHttpClientFactory httpClientFactory,
    IKaleidoCorrelationContextAccessor correlation,
    ICorrelationHeaderStamper headerStamper,
    KaleidoProcessClientRouteOptionsMap routeOptionsMap)
    : KaleidoClientFactoryBase<IKaleidoProcessClient, KaleidoProcessClientRouteOptionsMap>,
      IKaleidoProcessClientFactory
{
    protected override IHttpClientFactory HttpClientFactory => httpClientFactory;
    protected override IKaleidoCorrelationContextAccessor CorrelationAccessor => correlation;
    protected override ICorrelationHeaderStamper HeaderStamper => headerStamper;
    protected override KaleidoProcessClientRouteOptionsMap RouteOptionsMap => routeOptionsMap;

    protected override IKaleidoProcessClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        ICorrelationHeaderStamper stamper,
        string serviceName)
    {
        return new KaleidoProcessClient(httpClient, stamper, serviceName);
    }
}
