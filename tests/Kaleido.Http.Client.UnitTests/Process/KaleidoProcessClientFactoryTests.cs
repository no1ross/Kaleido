using Kaleido.Http.Process;
using Kaleido.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.UnitTests.Process;

public sealed class KaleidoProcessClientFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoProcessClientFactory CreateSut(
        IHttpClientFactory httpClientFactory,
        KaleidoProcessClientRouteOptionsMap routeOptionsMap) =>
        new(
            httpClientFactory,
            Mock.Of<ICorrelationHeaderStamper>(),
            NullLogger<KaleidoProcessClient>.Instance,
            routeOptionsMap);
    [Fact]
    public void GetClient_WhenNamedClientIsRegistered_ReturnsClient()
    {
        var routeMap = new KaleidoProcessClientRouteOptionsMap();
        routeMap.Options["remote-svc"] = "remote-svc";

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new System.Net.Http.HttpClient());

        var factory = CreateSut(httpClientFactory.Object, routeMap);

        var client = factory.GetClient("remote-svc");

        Assert.NotNull(client);
    }
}
