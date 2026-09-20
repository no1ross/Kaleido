namespace Kaleido.Http.Client.Queryable;

internal sealed class KaleidoQueryableClientFactory(
    IHttpClientFactory httpClientFactory,
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoQueryableClientRouteOptionsMap routeOptionsMap)
    : KaleidoClientFactoryBase<IKaleidoQueryableClient, KaleidoQueryableClientRouteOptionsMap>,
      IKaleidoQueryableClientFactory
{
    protected override IHttpClientFactory HttpClientFactory => httpClientFactory;
    protected override IKaleidoCorrelationContextAccessor CorrelationAccessor => correlation;
    protected override KaleidoQueryableClientRouteOptionsMap RouteOptionsMap => routeOptionsMap;

    protected override IKaleidoQueryableClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        IKaleidoCorrelationContextAccessor correlationAccessor,
        string serviceName)
    {
        return new KaleidoQueryableClient(httpClient, correlationAccessor, serviceName);
    }
}
