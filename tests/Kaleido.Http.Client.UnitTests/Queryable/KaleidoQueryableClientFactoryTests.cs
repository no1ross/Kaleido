using Kaleido.Http.Client;
using Kaleido.Http.Client.Queryable;
using Kaleido.Http.Queryable;
using Kaleido.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.UnitTests.Queryable;

public sealed class KaleidoQueryableClientFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoQueryableClientFactory CreateSut(
        IHttpClientFactory httpClientFactory,
        KaleidoQueryableClientRouteOptionsMap routeOptionsMap) =>
        new(
            httpClientFactory,
            Mock.Of<ICorrelationHeaderStamper>(),
            NullLogger<KaleidoQueryableClient>.Instance,
            routeOptionsMap,
            new KaleidoRemoteRegistry(
                httpClientFactory,
                Mock.Of<IRegistrySnapshotStore>(),
                NullLogger<KaleidoRemoteRegistry>.Instance));
    [Fact]
    public void GetClient_WhenNamedClientIsRegistered_ReturnsClient()
    {
        var routeMap = new KaleidoQueryableClientRouteOptionsMap();
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
