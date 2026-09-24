using Kaleido.Http.Client;
using Kaleido.Http.Client.Process;
using Kaleido.Http.Process;
using Kaleido.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Process.Http.Client.Tests;

public sealed class KaleidoProcessClientFactoryTests
{
    [Fact]
    public void GetClient_WhenNamedClientIsRegistered_ReturnsClient()
    {
        var routeMap = new KaleidoProcessClientRouteOptionsMap();
        routeMap.Options["remote-svc"] = "remote-svc";

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new System.Net.Http.HttpClient());

        var factory = new KaleidoProcessClientFactory(
            httpClientFactory.Object,
            Mock.Of<IKaleidoCorrelationContextAccessor>(),
            Mock.Of<ICorrelationHeaderStamper>(),
            NullLogger<KaleidoProcessClient>.Instance,
            routeMap);

        var client = factory.GetClient("remote-svc");

        Assert.NotNull(client);
    }
}
