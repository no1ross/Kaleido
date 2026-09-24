using Kaleido.Http.Client;
using Kaleido.Http.Client.Queryable;
using Kaleido.Http.Queryable;
using Kaleido.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Queryable.Http.Client.Tests;

public sealed class KaleidoQueryableClientFactoryTests
{
    [Fact]
    public void GetClient_WhenNamedClientIsRegistered_ReturnsClient()
    {
        var routeMap = new KaleidoQueryableClientRouteOptionsMap();
        routeMap.Options["remote-svc"] = "remote-svc";

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new System.Net.Http.HttpClient());

        var factory = new KaleidoQueryableClientFactory(
            httpClientFactory.Object,
            Mock.Of<IKaleidoCorrelationContextAccessor>(),
            Mock.Of<ICorrelationHeaderStamper>(),
            NullLogger<KaleidoQueryableClient>.Instance,
            routeMap);

        var client = factory.GetClient("remote-svc");

        Assert.NotNull(client);
    }
}
