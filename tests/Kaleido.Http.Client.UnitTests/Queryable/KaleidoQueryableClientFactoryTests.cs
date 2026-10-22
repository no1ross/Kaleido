using Kaleido.Exceptions;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.Queryable.UnitTests;

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
                new KaleidoServiceOptions { ServiceName = "consumer" },
                NullLogger<KaleidoRemoteRegistry>.Instance));

    [Fact]
    public void GetClient_ForAnUnconfiguredName_FailsNamingTheConfiguredClients()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();

        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => CreateSut(httpClientFactory.Object, new KaleidoQueryableClientRouteOptionsMap()).GetClient("Member"));

        Assert.Equal(HttpClientErrorCodes.UnknownClient, exception.Code);
        Assert.Equal("Kaleido client 'Member' is not configured under Kaleido:Clients; no clients are configured.", exception.Message);
        httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetClient_WhenNamedClientIsRegistered_ReturnsClient()
    {
        var routeMap = new KaleidoQueryableClientRouteOptionsMap();
        routeMap.SetRoutePrefix("remote-svc", "remote-svc");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new System.Net.Http.HttpClient());

        var factory = CreateSut(httpClientFactory.Object, routeMap);

        var client = factory.GetClient("remote-svc");

        Assert.NotNull(client);
    }

    [Fact]
    public void TryGetClientForService_FindsTheClientByItsRoutePrefix()
    {
        var routeMap = new KaleidoQueryableClientRouteOptionsMap();
        routeMap.SetRoutePrefix("MemberService", "member");

        using var httpClient = new System.Net.Http.HttpClient();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var factory = CreateSut(httpClientFactory.Object, routeMap);

        Assert.True(factory.TryGetClientForService("member", out var client));
        Assert.NotNull(client);
        httpClientFactory.Verify(f => f.CreateClient("MemberService"));
    }

    [Fact]
    public void TryGetClientForService_ForAServiceWithoutAClient_ReturnsFalse()
    {
        var factory = CreateSut(Mock.Of<IHttpClientFactory>(), new KaleidoQueryableClientRouteOptionsMap());

        Assert.False(factory.TryGetClientForService("codeset", out var client));
        Assert.Null(client);
    }
}
