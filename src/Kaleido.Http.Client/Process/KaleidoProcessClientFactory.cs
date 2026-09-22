namespace Kaleido.Http.Client.Process;

internal sealed class KaleidoProcessClientFactory(
    IHttpClientFactory httpClientFactory,
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoProcessClientRouteOptionsMap routeOptionsMap)
    : KaleidoClientFactoryBase<IKaleidoProcessClient, KaleidoProcessClientRouteOptionsMap>,
      IKaleidoProcessClientFactory
{
    protected override IHttpClientFactory HttpClientFactory => httpClientFactory;
    protected override IKaleidoCorrelationContextAccessor CorrelationAccessor => correlation;
    protected override KaleidoProcessClientRouteOptionsMap RouteOptionsMap => routeOptionsMap;

    protected override IKaleidoProcessClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        IKaleidoCorrelationContextAccessor correlationAccessor,
        string serviceName)
    {
        return new KaleidoProcessClient(httpClient, correlationAccessor, serviceName);
    }
}
